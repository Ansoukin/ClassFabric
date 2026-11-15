using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Media;
using ClassIsland.Core.Abstractions.Services;
using static FancyAnima.Overlay.NativeMethods;

namespace FancyAnima.Overlay;

/// <summary>
/// 全屏涟漪特效覆盖窗口 —— GDI UpdateLayeredWindow 渲染版。
///
/// 渲染链路演进：
///   - DirectComposition（IDCompositionSurface）：部分系统 DWM 完全不合成其内容
///     （最小纯色测试也不显示，API 全部返回成功但屏幕无输出），系统环境问题；
///   - HWND swapchain：不支持透明 alpha（Premultiplied 仅限 DComp/CoreWindow）；
///   - D3D11 GPU 绘制 + 读回 + ULW：GPU 版在 ClassIsland 环境不显示（原因待查），
///     且 D3DCompiler 运行时编译增加脆弱性；
///   - 最终（当前）：**GDI 纯 CPU 绘制 + UpdateLayeredWindow 提交** ——
///     Win2000 到 Win11 全支持的成熟透明覆盖层路径，逐像素 alpha 混合，
///     在任何系统上都 100% 可显示，无 GPU/合成器依赖。
///
/// 性能设计：
///   - 独立线程 + 窗口池（只建一次，动画结束移到屏幕外隐藏复用）+ 预热；
///   - 脏矩形：涟漪同心扩散，每帧只清+画当前圆环外接矩形；
///   - 预乘 alpha：画布输出 straight alpha，像素直接用预乘色，
///     保证 UpdateLayeredWindow(AC_SRC_ALPHA) 混合正确无白边；
///   - 帧率上限：动画仅 750ms，30fps 足够顺滑，同时把 CPU 占用压到最低。
///
/// 鼠标穿透：WS_EX_LAYERED | WS_EX_TRANSPARENT + WM_NCHITTEST 返回 HTTRANSPARENT。
/// </summary>
public class FancyAnimaRippleOverlay : IDisposable
{
    private static readonly WndProcDelegate s_wndProcDelegate = WndProc;

    private readonly object _gate = new();
    private Thread? _thread;
    private volatile bool _running;
    private volatile bool _disposed;
    private volatile bool _initFailed;

    // ---- 跨线程传递的触发参数（UI 线程写入，渲染线程消费）----
    private IntPtr _pendingMainWindow;
    private Rgba4 _pendingColor;
    private int _pendingFps;
    private AnimationQuality _pendingQuality;
    private TaskCompletionSource? _pendingTcs;
    private bool _hasPendingTrigger;
    private bool _cancelRequested;

    // ---- 渲染线程资源（只在渲染线程上访问）----
    private IntPtr _hwnd;
    private IntPtr _screenDc;
    private IntPtr _dibDc;
    private IntPtr _dibSection;
    private IntPtr _dibBits;

    // ---- 动画状态（渲染线程）----
    private readonly Stopwatch _animClock = new();
    private bool _animating;
    private bool _firstFrameAfterShow;
    private System.Numerics.Vector2 _center;
    private float _maxRadius;
    private int _durationMs;
    private int _frameIntervalMs = 33;
    private float _nextFrameMs;
    private float _lastTopmostMs;
    private int _windowX;
    private int _windowY;
    private byte _rippleR;
    private byte _rippleG;
    private byte _rippleB;
    private AnimationQuality _quality;
    private float _baseAlpha;
    private int _surfaceWidth;
    private int _surfaceHeight;
    private TaskCompletionSource? _activeTcs;

    // ---- 帧耗时统计（动画结束时打印，用于性能诊断）----
    private readonly Stopwatch _frameSw = new();
    private int _frameCount;
    private float _frameMsSum;
    private float _frameMsMax;

    /// <summary>起始透明度</summary>
    private const float BaseAlpha = 0.95f;

    /// <summary>
    /// 0..1 浮点 RGBA 颜色。原实现用 Vortice.Mathematics.Color4，
    /// 只为这一个结构引整个依赖不值，内联同形结构。
    /// </summary>
    private readonly record struct Rgba4(float R, float G, float B, float A);

    public FancyAnimaRippleOverlay(int fpsLimit, AnimationQuality quality)
    {
        _pendingFps = Math.Clamp(fpsLimit, 1, 120);
        _pendingQuality = quality;
    }

    /// <summary>
    /// 日志回调（由宿主接入 ClassIsland 日志系统；不设置则退回 Console）。
    /// </summary>
    public Action<string>? Log { get; set; }

    private void Trace(string msg)
    {
        Log?.Invoke(msg);
        Console.WriteLine(msg);
    }

    // ============================================================
    //  对外 API（线程安全）
    // ============================================================

    /// <summary>预热：启动渲染线程并提前创建窗口与位图资源（幂等）。</summary>
    public void Prewarm()
    {
        lock (_gate)
        {
            if (_thread is not null) return;
            _running = true;
            _thread = new Thread(RenderLoop)
            {
                IsBackground = true,
                Name = "FancyAnimaOverlay",
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }
    }

    /// <summary>播放一次涟漪特效。返回的 Task 在动画结束时完成。</summary>
    public Task PlayAsync(
        IntPtr mainWindowHwnd,
        Avalonia.Media.Color themeColor,
        int fpsLimit,
        AnimationQuality quality,
        CancellationToken ct = default)
        => PlayAsync(mainWindowHwnd, ToRgba4(themeColor), fpsLimit, quality, ct);

    /// <summary>Rgba4 重载：不依赖 Avalonia 类型，便于独立测试/复用。</summary>
    private Task PlayAsync(
        IntPtr mainWindowHwnd,
        Rgba4 color,
        int fpsLimit,
        AnimationQuality quality,
        CancellationToken ct = default)
    {
        // 防御：没预热过也能播（首帧会现建，略卡但不会挂）
        Prewarm();

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (_initFailed)
            {
                // 渲染线程初始化失败（GDI 不可用等），立即完成，避免调用方永久挂起
                return Task.CompletedTask;
            }

            _pendingMainWindow = mainWindowHwnd;
            _pendingColor = color;
            _pendingFps = Math.Clamp(fpsLimit, 1, 120);
            _pendingQuality = quality;
            _pendingTcs = tcs;
            _hasPendingTrigger = true;
        }

        if (ct.CanBeCanceled)
        {
            ct.Register(() =>
            {
                lock (_gate) { _cancelRequested = true; }
            });
        }

        return tcs.Task;
    }

    /// <summary>卸载时调用：取消播放并真正销毁窗口与资源。</summary>
    public void Shutdown() => Dispose();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _running = false;
        _thread?.Join(2000);
        _thread = null;
    }

    // ============================================================
    //  渲染线程
    // ============================================================

    private void RenderLoop()
    {
        try
        {
            InitializeResources();
        }
        catch (Exception ex)
        {
            Trace($"[FancyAnima] 渲染资源初始化失败：{ex}");
            lock (_gate) { _initFailed = true; }
            CompleteActiveTask();
            CompletePendingTask();
            return;
        }

        try
        {
            while (_running)
            {
                PumpMessages();

                // ---- 取触发任务 ----
                PendingTrigger? pending = null;
                lock (_gate)
                {
                    if (_hasPendingTrigger)
                    {
                        pending = new PendingTrigger(
                            _pendingMainWindow, _pendingColor, _pendingFps, _pendingQuality);
                        _activeTcs = _pendingTcs;
                        _pendingTcs = null;
                        _hasPendingTrigger = false;
                        _cancelRequested = false;
                    }
                }

                if (pending.HasValue)
                {
                    if (_animating) EndAnimation();          // 上一段还没放完：立即收尾
                    if (!BeginAnimation(pending.Value))      // 定位失败：直接完成
                        CompleteActiveTask();
                }

                // ---- 取消请求 → 提前结束 ----
                bool cancel;
                lock (_gate) { cancel = _cancelRequested; }
                if (cancel && _animating) EndAnimation();

                // ---- 帧循环 ----
                if (_animating)
                {
                    var nowMs = (float)_animClock.ElapsedMilliseconds;

                    // 动画期间周期性重新置顶，防止被 ClassIsland 官方通知窗口盖住
                    if (nowMs - _lastTopmostMs >= 300f)
                    {
                        _lastTopmostMs = nowMs;
                        SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                    }

                    if (nowMs >= _nextFrameMs)
                    {
                        _nextFrameMs = nowMs + _frameIntervalMs;
                        try
                        {
                            RenderFrame(nowMs);
                        }
                        catch (Exception ex)
                        {
                            // 单帧失败不杀线程：记日志并收尾本次动画，线程继续待命
                            Trace($"[FancyAnima] 渲染帧异常：{ex}");
                            EndAnimation();
                        }
                    }

                    if (_animClock.ElapsedMilliseconds >= _durationMs)
                    {
                        EndAnimation();
                    }
                    else
                    {
                        // 精确等待到下一帧时刻（timeBeginPeriod 后 Sleep 精度 1ms，
                        // 帧间隔均匀，动画速度稳定不卡顿）
                        var waitMs = _nextFrameMs - (float)_animClock.ElapsedMilliseconds;
                        if (waitMs > 1.5f)
                            Thread.Sleep((int)MathF.Ceiling(waitMs - 0.5f));
                        else
                            Thread.Yield();
                    }
                }
                else
                {
                    Thread.Sleep(15); // 空闲时降低 CPU 占用
                }
            }
        }
        catch (Exception ex)
        {
            Trace($"[FancyAnima] 渲染线程异常：{ex}");
            CompleteActiveTask();
            CompletePendingTask();
        }
        finally
        {
            CleanupResources();
        }
    }

    private void PumpMessages()
    {
        while (PeekMessageW(out var msg, IntPtr.Zero, 0, 0, PM_REMOVE))
        {
            if (msg.message == WM_QUIT)
            {
                _running = false;
                break;
            }

            TranslateMessage(ref msg);
            DispatchMessageW(ref msg);
        }
    }

    private void CompleteActiveTask()
    {
        TaskCompletionSource? tcs;
        lock (_gate) { tcs = _activeTcs; _activeTcs = null; }
        tcs?.TrySetResult();
    }

    private void CompletePendingTask()
    {
        TaskCompletionSource? tcs;
        lock (_gate)
        {
            tcs = _pendingTcs;
            _pendingTcs = null;
            _hasPendingTrigger = false;
        }
        tcs?.TrySetResult();
    }

    // ============================================================
    //  资源初始化（预热）
    // ============================================================

    private void InitializeResources()
    {
        // 1. 创建隐藏覆盖窗口
        _hwnd = CreateOverlayWindow();

        // 2. 屏幕 DC（UpdateLayeredWindow 需要）
        _screenDc = GetDC(IntPtr.Zero);
        if (_screenDc == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        // 3. 预热：先建一块默认尺寸的 DIB（真正播放时按显示器尺寸重建）
        EnsureSurface(1920, 1080);

        Trace("[FancyAnima] 覆盖窗口与 GDI 渲染资源已预热完成");
    }

    private void EnsureSurface(int width, int height)
    {
        if (_dibBits != IntPtr.Zero && _surfaceWidth == width && _surfaceHeight == height)
            return;

        DisposeSurface();

        _surfaceWidth = width;
        _surfaceHeight = height;

        // top-down DIB（负高度）→ 坐标与窗口一致（0,0 在左上）
        var bmi = new BITMAPINFO
        {
            bmiHeader = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = width,
                biHeight = -height,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = BI_RGB
            }
        };

        _dibDc = CreateCompatibleDC(IntPtr.Zero);
        if (_dibDc == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        _dibSection = CreateDIBSection(_dibDc, ref bmi, DIB_RGB_COLORS, out _dibBits, IntPtr.Zero, 0);
        if (_dibSection == IntPtr.Zero || _dibBits == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        SelectObject(_dibDc, _dibSection);

        // 初始全透明
        unsafe
        {
            new Span<byte>((void*)_dibBits, width * height * 4).Clear();
        }
    }

    private void DisposeSurface()
    {
        if (_dibSection != IntPtr.Zero)
        {
            DeleteObject(_dibSection);
            _dibSection = IntPtr.Zero;
        }
        if (_dibDc != IntPtr.Zero)
        {
            DeleteDC(_dibDc);
            _dibDc = IntPtr.Zero;
        }
        _dibBits = IntPtr.Zero;
    }

    // ============================================================
    //  动画
    // ============================================================

    private bool BeginAnimation(PendingTrigger state)
    {
        // 主窗口矩形 → 几何中心（屏幕坐标，物理像素）。
        // 主窗口最小化/隐藏时 GetWindowRect 会失败或返回 (-32000,-32000)，
        // 此时回退到主显示器中心，避免涟漪在屏幕外播放（表现为"没特效"）。
        var rcValid = GetWindowRect(state.MainWindow, out var rc)
                      && rc.Left > -30000 && rc.Top > -30000;

        IntPtr monitor;
        float centerX, centerY;
        if (rcValid)
        {
            centerX = (rc.Left + rc.Right) / 2f;
            centerY = (rc.Top + rc.Bottom) / 2f;
            monitor = MonitorFromWindow(state.MainWindow, MONITOR_DEFAULTTONEAREST);
        }
        else
        {
            monitor = MonitorFromPoint(new POINT { X = 0, Y = 0 }, MONITOR_DEFAULTTOPRIMARY);
            var miPrimary = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfoW(monitor, ref miPrimary))
                return false;
            var mr = miPrimary.rcMonitor;
            centerX = (mr.Left + mr.Right) / 2f;
            centerY = (mr.Top + mr.Bottom) / 2f;
        }

        // 涟漪所在显示器
        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfoW(monitor, ref mi))
            return false;

        var m = mi.rcMonitor;
        var w = m.Right - m.Left;
        var h = m.Bottom - m.Top;
        if (w <= 0 || h <= 0)
            return false;

        EnsureSurface(w, h);
        _windowX = m.Left;
        _windowY = m.Top;

        // 窗口移到目标显示器并置顶显示（不抢焦点）
        SetWindowPos(_hwnd, HWND_TOPMOST, m.Left, m.Top, w, h, SWP_SHOWWINDOW | SWP_NOACTIVATE);
        ShowWindow(_hwnd, SW_SHOWNA);
        Trace($"[FancyAnima] 涟漪窗口显示: {w}x{h} @ ({m.Left},{m.Top}) 可见={IsWindowVisible(_hwnd)} rect有效={rcValid}");

        // 动画参数：涟漪从主界面几何中心扩散，最大半径盖满整个显示器
        _center = new System.Numerics.Vector2(centerX - m.Left, centerY - m.Top);
        var dx = Math.Max(_center.X, w - _center.X);
        var dy = Math.Max(_center.Y, h - _center.Y);
        _maxRadius = MathF.Sqrt(dx * dx + dy * dy);
        _rippleR = (byte)(state.Color.R * 255f);
        _rippleG = (byte)(state.Color.G * 255f);
        _rippleB = (byte)(state.Color.B * 255f);
        _quality = state.Quality;
        _durationMs = GetDurationMs(state.Quality);
        _baseAlpha = BaseAlpha * GetQualityAlphaScale(state.Quality);
        _frameIntervalMs = 1000 / Math.Max(1, state.Fps);
        // 提升系统时钟精度（1ms），保证 Sleep 精确、帧间隔均匀
        timeBeginPeriod(1);
        _nextFrameMs = 0;
        _firstFrameAfterShow = true;
        _frameCount = 0;
        _frameMsSum = 0f;
        _frameMsMax = 0f;
        _animClock.Restart();
        _animating = true;
        return true;
    }

    private void RenderFrame(float nowMs)
    {
        if (_dibBits == IntPtr.Zero)
            return;

        _frameSw.Restart();

        // t ∈ [0,1]
        var t = Math.Clamp(nowMs / _durationMs, 0f, 1f);
        // 半径：由快到慢（ease-out cubic）
        var eased = 1f - MathF.Pow(1f - t, 3f);
        // 透明度：前期保持高可见度，最后阶段才快速淡出（t² 缓降）
        var alpha = _baseAlpha * (1f - t * t);
        var radius = MathF.Max(1f, _maxRadius * eased);

        var cx = _center.X;
        var cy = _center.Y;

        // ---- 脏矩形（同心扩散：新圆完全覆盖旧圆，无需清屏，只需画新圆）----
        var bx0 = (int)Math.Clamp(cx - radius, 0, _surfaceWidth);
        var by0 = (int)Math.Clamp(cy - radius, 0, _surfaceHeight);
        var bx1 = (int)Math.Clamp(cx + radius + 1f, 0, _surfaceWidth);
        var by1 = (int)Math.Clamp(cy + radius + 1f, 0, _surfaceHeight);

        if (_firstFrameAfterShow)
        {
            // 窗口刚显示：整窗清透明一次，避免残留上一轮内容
            _firstFrameAfterShow = false;
            unsafe
            {
                new Span<byte>((void*)_dibBits, _surfaceWidth * _surfaceHeight * 4).Clear();
            }
            CommitLayeredWindow(0, 0, _surfaceWidth, _surfaceHeight);
        }

        if (bx1 <= bx0 || by1 <= by0)
            return;

        // ---- 逐行 span 直接写 DIB 内存（比 GDI+ 快数倍：纯内存 memset 级别）----
        // 预乘 alpha（ULW AC_SRC_ALPHA 要求）：r/g/b 已乘 alpha
        var a = (int)(alpha * 255f);
        var r = _rippleR * a / 255;
        var g = _rippleG * a / 255;
        var b = _rippleB * a / 255;
        // DIB 32bpp 内存字节序 = B,G,R,A（低字节 Blue）。little-endian 下
        // (a<<24)|(r<<16)|(g<<8)|b 写入内存后 [0]=b,[1]=g,[2]=r,[3]=a ✓ BGRA
        var color = (uint)((a << 24) | (r << 16) | (g << 8) | b);   // BGRA

        var r2 = radius * radius;
        var yMin = Math.Max(by0, (int)(cy - radius));
        var yMax = Math.Min(by1, (int)(cy + radius + 1f));
        var stride = _surfaceWidth * 4;

        unsafe
        {
            var dstBase = (byte*)_dibBits;
            for (var y = yMin; y < yMax; y++)
            {
                var dy = y + 0.5f - cy;                             // 像素中心
                var dx = MathF.Sqrt(Math.Max(0f, r2 - dy * dy));
                var x0 = (int)MathF.Ceiling(cx - dx);
                var x1 = (int)MathF.Floor(cx + dx);
                if (x1 < x0) continue;
                if (x0 < 0) x0 = 0;
                if (x1 >= _surfaceWidth) x1 = _surfaceWidth - 1;
                if (x1 < x0) continue;

                var p = (uint*)(dstBase + y * stride + x0 * 4);
                var len = x1 - x0 + 1;
                for (var i = 0; i < len; i++)
                    p[i] = color;
            }
        }

        // ---- 提交（只更新脏矩形）----
        CommitLayeredWindow(bx0, by0, bx1, by1);

        var elapsed = (float)_frameSw.Elapsed.TotalMilliseconds;
        _frameCount++;
        _frameMsSum += elapsed;
        if (elapsed > _frameMsMax) _frameMsMax = elapsed;
    }

    /// <summary>把 DIB 内容提交到屏幕（普通 UpdateLayeredWindow 全窗提交，兼容性最稳）。</summary>
    private void CommitLayeredWindow(int x, int y, int right, int bottom)
    {
        if (_dibDc == IntPtr.Zero || _screenDc == IntPtr.Zero)
            return;

        var dst = new POINT { X = _windowX, Y = _windowY };
        var size = new SIZE { Width = _surfaceWidth, Height = _surfaceHeight };
        var src = new POINT { X = 0, Y = 0 };
        var blend = new BLENDFUNCTION
        {
            BlendOp = AC_SRC_OVER,
            BlendFlags = 0,
            SourceConstantAlpha = 255,
            AlphaFormat = AC_SRC_ALPHA
        };

        UpdateLayeredWindow(_hwnd, _screenDc, ref dst, ref size, _dibDc, ref src, 0, ref blend, ULW_ALPHA);
    }

    private void EndAnimation()
    {
        _animating = false;

        if (_frameCount > 0)
        {
            Trace($"[FancyAnima] 本次动画 {_frameCount} 帧，平均 {_frameMsSum / _frameCount:F1}ms/帧，最大 {_frameMsMax:F1}ms/帧");
        }

        // 还原系统时钟精度
        timeEndPeriod(1);

        // 窗口池：不销毁窗口，移到屏幕外并隐藏，下次直接复用
        SetWindowPos(_hwnd, HWND_TOPMOST, -32000, -32000, _surfaceWidth, _surfaceHeight, SWP_HIDEWINDOW | SWP_NOACTIVATE);
        ShowWindow(_hwnd, SW_HIDE);

        CompleteActiveTask();
    }

    /// <summary>三档时长统一（档位只区别透明度）。</summary>
    private static int GetDurationMs(AnimationQuality quality) => 750;

    /// <summary>档位透明度系数：简化=淡，精细=浓。</summary>
    private static float GetQualityAlphaScale(AnimationQuality quality) => quality switch
    {
        AnimationQuality.Simplified => 0.7f,
        AnimationQuality.Fine => 1.0f,
        _ => 0.85f,
    };

    private static Rgba4 ToRgba4(Avalonia.Media.Color c) =>
        new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);


    // ============================================================
    //  窗口创建
    // ============================================================

    private static IntPtr CreateOverlayWindow()
    {
        const string className = "FancyAnimaRippleOverlay";
        var hInstance = Process.GetCurrentProcess().Handle;

        var wndClass = new WNDCLASS
        {
            lpfnWndProc = s_wndProcDelegate,
            hInstance = hInstance,
            lpszClassName = className,
        };

        if (RegisterClassW(ref wndClass) == 0)
        {
            var err = Marshal.GetLastWin32Error();
            if (err != ERROR_CLASS_ALREADY_EXISTS)
                throw new Win32Exception(err);
        }

        var hwnd = CreateWindowExW(
            WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_LAYERED | WS_EX_TRANSPARENT,
            className,
            "FancyAnimaRippleOverlay",
            WS_POPUP,
            -32000, -32000, 1, 1,
            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);

        if (hwnd == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        return hwnd;
    }

    private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_NCHITTEST:
                // 鼠标穿透（与 WS_EX_TRANSPARENT 双保险）
                return new IntPtr(HTTRANSPARENT);
            case WM_ERASEBKGND:
                return new IntPtr(1);
            default:
                return DefWindowProcW(hWnd, msg, wParam, lParam);
        }
    }

    private void CleanupResources()
    {
        DisposeSurface();
        if (_screenDc != IntPtr.Zero)
        {
            ReleaseDC(IntPtr.Zero, _screenDc);
            _screenDc = IntPtr.Zero;
        }
        if (_hwnd != IntPtr.Zero)
        {
            DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
    }

    private readonly record struct PendingTrigger(
        IntPtr MainWindow,
        Rgba4 Color,
        int Fps,
        AnimationQuality Quality);
}

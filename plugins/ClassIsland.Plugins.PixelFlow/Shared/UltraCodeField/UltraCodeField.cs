using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace ClassIsland.UltraCodeShared;

/// <summary>
/// Claude UltraCode 风格的像素场动画。
/// </summary>
/// <remarks>
/// 保持和宿主 <c>SlantedMaskControl</c> 相同的 <see cref="IsOpened"/> 契约，
/// 在 UltraCode 主题里可以直接互换。
/// <para/>
/// <b>这份源码被三个插件共享</b>（UltraCode / 值日提醒 / 课后自动复原）。
/// 它是纯过程式绘制，没有任何 <c>avares://</c> 资源依赖，所以按源码分发是干净的；
/// 而按 dll 共享会因为每个插件有独立的 PluginLoadContext 而拿到不同的程序集身份。
/// <para/>
/// 揭示方向：<c>frontier = 1 - reveal</c>，reveal 从 0 到 1 时 frontier 从 1 到 0，
/// 点亮区域<b>由右向左</b>扩张；<see cref="Collapse"/> 反向播放即「从右往左流走」。
/// </remarks>
public class UltraCodeField : Control
{
    // 每个格子在位图中占 SubSamples×SubSamples 个像素，其中内部 SubSamples-1 用于填充，
    // 剩下一行一列充当格子间隙。6/5 的比例对应原实现的 cell=6px、gap=1.1px。
    private const int SubSamples = 6;
    private const int SubFill = SubSamples - 1;

    private const double BaseFlowDurationMs = 4000.0;
    private const double EdgeFeather = 22.0;

    // 倒计时收边的羽化宽度。比入场羽化窄得多——这条边是要被当成刻度读的，糊了就没法读了。
    private const double DrainFeather = 8.0;

    // 收边前沿那圈「燃烧」高光的宽度（归一化坐标）与低频进度值的缓动时间常数。
    private const double BurnWidth = 0.045;
    private const double DrainEaseMs = 120.0;

    /// <summary>像素场底下的连续渐变。主界面其余部分需要与遮罩衔接时可直接复用。</summary>
    /// <remarks>颜色由 <see cref="UltraCodePalette"/> 从应用主题色推导，实例本身不会被替换。</remarks>
    public static IBrush BaseGradient => UltraCodePalette.BaseGradient;

    // 提醒面板的不透明度**不再**跟随主界面的「背景不透明度」，改由插件设置里的
    // 「动画不透明度」单独控制（见 UltraCodeOptions.AnimationOpacity）。
    // 原因：跟随的时候把主界面调透一点，提醒动画就跟着淡到几乎看不见了，
    // 但提醒本来就是要被看清的，这两件事不该绑在一起。

    public static readonly StyledProperty<bool> IsOpenedProperty =
        AvaloniaProperty.Register<UltraCodeField, bool>(nameof(IsOpened));

    public static readonly StyledProperty<double> CellSizeProperty =
        AvaloniaProperty.Register<UltraCodeField, double>(nameof(CellSize), 6.0);

    public static readonly StyledProperty<int> RevealDurationMsProperty =
        AvaloniaProperty.Register<UltraCodeField, int>(nameof(RevealDurationMs), 600);

    public static readonly StyledProperty<int> CollapseDurationMsProperty =
        AvaloniaProperty.Register<UltraCodeField, int>(nameof(CollapseDurationMs), 360);

    public static readonly StyledProperty<double> PixelOpacityProperty =
        AvaloniaProperty.Register<UltraCodeField, double>(nameof(PixelOpacity), 1.0);

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<UltraCodeField, double>(nameof(Progress), double.NaN);

    public static readonly StyledProperty<double> CellSizeOverrideProperty =
        AvaloniaProperty.Register<UltraCodeField, double>(nameof(CellSizeOverride), double.NaN);

    public bool IsOpened
    {
        get => GetValue(IsOpenedProperty);
        set => SetValue(IsOpenedProperty, value);
    }

    /// <summary>单个像素格的边长（控件坐标）。</summary>
    public double CellSize
    {
        get => GetValue(CellSizeProperty);
        set => SetValue(CellSizeProperty, value);
    }

    public int RevealDurationMs
    {
        get => GetValue(RevealDurationMsProperty);
        set => SetValue(RevealDurationMsProperty, value);
    }

    public int CollapseDurationMs
    {
        get => GetValue(CollapseDurationMsProperty);
        set => SetValue(CollapseDurationMsProperty, value);
    }

    /// <summary>
    /// 闪烁像素层的整体不透明度，底色渐变不受影响。
    /// 长时间挂在屏幕上的场景（比如上课倒计时）调低一些，文字才不会被闪烁盖过去。
    /// </summary>
    public double PixelOpacity
    {
        get => GetValue(PixelOpacityProperty);
        set => SetValue(PixelOpacityProperty, value);
    }

    /// <summary>
    /// 剩余进度（<c>1 → 0</c>）。绑上之后整条像素带会随剩余时间从右往左收，
    /// 收边处保留一圈高亮的「燃烧前沿」，让长度本身成为倒计时的读数。
    /// </summary>
    /// <remarks>
    /// 默认 <see cref="double.NaN"/>，表示不参与倒计时，像素场铺满整个控件——
    /// 遮罩那一层就走这条路径。
    /// </remarks>
    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <summary>
    /// 强制指定像素格边长，压过 UltraCode 设置里的那个值。
    /// </summary>
    /// <remarks>
    /// <see cref="double.NaN"/>（默认）表示不干预，仍然听 UltraCode 设置页的。
    /// <para/>
    /// <b>这是给铺满整屏的场景用的。</b>每帧的开销和格子数成正比，而格子数是
    /// <c>宽 × 高 ÷ 边长²</c>——边长翻倍，开销降到四分之一。
    /// 提醒条只有几百像素宽，边长 6 无所谓；整屏就完全是另一回事：
    /// 3840×2160 的屏在边长 6 时每帧要算 23 万个格子，边长 12 只要不到 6 万。
    /// </remarks>
    public double CellSizeOverride
    {
        get => GetValue(CellSizeOverrideProperty);
        set => SetValue(CellSizeOverrideProperty, value);
    }

    /// <summary>
    /// 全局时钟，所有实例共用，而且永不重置。
    /// </summary>
    /// <remarks>
    /// 流动相位是从这个时钟算出来的。以前每个实例各有一个时钟、
    /// <see cref="Stop"/> 时还会 <c>Reset()</c>，于是每次重新打开、
    /// 或者换一个实例接着显示，流动的花纹都会跳回起点。
    /// 共用一个不重置的时钟，花纹就是连续的。
    /// </remarks>
    private static readonly Stopwatch SharedClock = Stopwatch.StartNew();

    /// <summary>
    /// 上一次有实例真的在画的时刻（共用时钟的毫秒数）。
    /// </summary>
    /// <remarks>
    /// 用来识别「交接」：提醒的遮罩层和正文层是两个不同的实例，
    /// 遮罩收走之后正文马上接上。这种情况下正文<b>不该再从右往左扫一遍</b>，
    /// 否则看起来就是同一个动画莫名其妙重播了。
    /// </remarks>
    private static double _lastActiveAt = double.NegativeInfinity;

    /// <summary>
    /// 判定「交接」的时间窗。遮罩收场 320 ms 左右，留一点富余。
    /// </summary>
    private const double HandoffWindowMs = 900.0;

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(33) };

    private WriteableBitmap? _bitmap;
    private int _bitmapColumns;
    private int _bitmapRows;

    private double _phaseStartedAt;
    private bool _collapsing;
    private bool _running;

    // 上一帧实际显示的剩余进度，以及取到它的时刻。
    private double _displayedDrain = 1.0;
    private double _drainSampledAt;

    public UltraCodeField()
    {
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
        _timer.Tick += (_, _) => InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenedProperty)
        {
            if (change.GetNewValue<bool>())
            {
                Open();
            }
            else
            {
                Collapse();
            }
        }
    }

    /// <summary>
    /// 收场动画播完时触发。
    /// </summary>
    /// <remarks>
    /// 用代码驱动这个控件的窗口（值日全屏提醒、复原遮罩）要等像素场真的流走了再关窗，
    /// 否则收场只播到一半就被窗口关闭截断了。
    /// </remarks>
    public event EventHandler? Collapsed;

    /// <summary>动画是不是正在跑。</summary>
    public bool IsRunning => _running;

    public void Open()
    {
        // 每次播提醒时重新取一次主题色。提醒本身是瞬时的，在这里刷新就不用额外挂主题变更事件，
        // 也不可能出现调色板和当前主题对不上的情况。
        UltraCodePalette.Refresh();

        var now = SharedClock.Elapsed.TotalMilliseconds;

        // 交接：刚刚还有别的实例在画（提醒的遮罩层收走、正文层接上），
        // 这种时候<b>不要再从右往左扫一遍</b>——那看起来就是同一个动画平白重播了一次。
        // 把相位直接拨到入场结束，正文一出现就是铺满的，只有流动在继续。
        var handoff = now - _lastActiveAt < HandoffWindowMs;

        _phaseStartedAt = handoff ? now - Math.Max(1, RevealDurationMs) : now;
        _collapsing = false;
        _running = true;
        // 直接对齐到当前进度，避免开场时收边从 100% 缓动下来白跑一段。
        _displayedDrain = ResolveTargetDrain();
        _drainSampledAt = now;
        _timer.Start();
        InvalidateVisual();
    }

    private double ResolveTargetDrain()
    {
        var progress = Progress;
        return double.IsNaN(progress) ? 1.0 : Math.Clamp(progress, 0.0, 1.0);
    }

    public void Collapse()
    {
        if (!_running)
        {
            return;
        }

        _phaseStartedAt = SharedClock.Elapsed.TotalMilliseconds;
        _collapsing = true;
        InvalidateVisual();
    }

    public void Stop()
    {
        _running = false;
        _timer.Stop();
        _bitmap?.Dispose();
        _bitmap = null;
        _bitmapColumns = _bitmapRows = 0;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Stop();
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var width = Bounds.Width;
        var height = Bounds.Height;
        if (!_running || width <= 0 || height <= 0)
        {
            return;
        }

        var now = SharedClock.Elapsed.TotalMilliseconds;
        var phaseElapsed = now - _phaseStartedAt;

        // 记下「这一帧确实画了东西」，下一个实例据此判断要不要跳过入场扫描。
        _lastActiveAt = now;

        double reveal;
        if (_collapsing)
        {
            var progress = Math.Clamp(phaseElapsed / Math.Max(1, CollapseDurationMs), 0.0, 1.0);
            reveal = 1.0 - Smoothstep(0, 1, progress);
            if (progress >= 1.0)
            {
                Stop();
                // 在 Render 里回调是安全的：Stop() 已经把状态清干净了，
                // 订阅方即使在这里关窗也不会再进到下一帧。
                Collapsed?.Invoke(this, EventArgs.Empty);
                return;
            }
        }
        else
        {
            reveal = Smoothstep(0, 1, phaseElapsed / Math.Max(1, RevealDurationMs));
        }

        var frontier = 1.0 - reveal;

        // 倒计时收边。进度是由主定时器低频推送的，直接用会一跳一跳，
        // 所以每帧朝目标缓动一小步，让边界始终是连续滑动的。
        var hasDrain = !double.IsNaN(Progress) && UltraCodeTuning.Current.CountdownDrain;
        var drain = ResolveTargetDrain();
        if (hasDrain)
        {
            var frameDelta = Math.Max(0.0, now - _drainSampledAt);
            _drainSampledAt = now;
            _displayedDrain += (drain - _displayedDrain) * Math.Clamp(frameDelta / DrainEaseMs, 0.0, 1.0);
            drain = _displayedDrain;
        }

        // 底色渐变：像素场之外的连续背景，保证遮罩能真正盖住下面的内容。
        // 渐变本身按控件整宽取样，只把已揭示的区域裁出来，避免颜色随进度被拉伸。
        var baseLeft = frontier * width;
        var baseRight = hasDrain ? drain * width : width;
        if (baseLeft < baseRight)
        {
            var clip = new Rect(baseLeft, 0, baseRight - baseLeft, height);
            // 推进边缘做一段羽化，让实底与前方零散亮起的像素格衔接，而不是一条硬扫描线。
            // 羽化宽度同时受两端距离约束，完全展开或完全收起时自然归零。
            var leftFeather = Math.Min(EdgeFeather, Math.Min(baseLeft, clip.Width));
            // 收边这侧羽化窄很多：它是拿来读剩余时间的，边界必须清楚。
            var rightFeather = hasDrain
                ? Math.Min(DrainFeather, Math.Min(width - baseRight, clip.Width))
                : 0.0;
            using (context.PushClip(clip))
            {
                if (leftFeather > 0.5 || rightFeather > 0.5)
                {
                    var fade = CreateEdgeFade(
                        leftFeather > 0.5 ? leftFeather / clip.Width : 0.0,
                        rightFeather > 0.5 ? rightFeather / clip.Width : 0.0);
                    using (context.PushOpacityMask(fade, clip))
                    {
                        context.DrawRectangle(BaseGradient, null, new Rect(0, 0, width, height));
                    }
                }
                else
                {
                    context.DrawRectangle(BaseGradient, null, new Rect(0, 0, width, height));
                }
            }
        }

        // 格子大小的优先级：控件上的强制值 > UltraCode 设置 > XAML 上写的。
        // 强制值是给整屏场景留的口子——那里格子数太多，必须能单独调粗来压计算量。
        var forcedCell = CellSizeOverride;
        var configuredCell = UltraCodeTuning.Current.CellSize;
        var cell = Math.Max(1.0,
            !double.IsNaN(forcedCell) && forcedCell > 0 ? forcedCell :
            configuredCell > 0 ? configuredCell : CellSize);
        var columns = (int)Math.Ceiling(width / cell);
        var rows = (int)Math.Ceiling(height / cell);
        if (columns <= 0 || rows <= 0)
        {
            return;
        }

        EnsureBitmap(columns, rows);
        if (_bitmap is null)
        {
            return;
        }

        // XAML 上的 PixelOpacity 是「这一层」的基准（遮罩亮、正文暗），
        // 再乘一个全局倍率，让用户能整体调闪烁的存在感。
        var pixelOpacity = Math.Clamp(
            PixelOpacity * Math.Clamp(UltraCodeTuning.Current.PixelIntensity, 0.0, 2.0), 0.0, 1.0);
        DrawPixelField(now, reveal, frontier, columns, rows, cell, width,
            pixelOpacity, hasDrain ? drain : double.NaN);

        context.DrawImage(_bitmap,
            new Rect(0, 0, columns * SubSamples, rows * SubSamples),
            new Rect(0, 0, columns * cell, rows * cell));
    }

    private void EnsureBitmap(int columns, int rows)
    {
        if (_bitmap is not null && _bitmapColumns == columns && _bitmapRows == rows)
        {
            return;
        }

        _bitmap?.Dispose();
        _bitmap = new WriteableBitmap(
            new PixelSize(columns * SubSamples, rows * SubSamples),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);
        _bitmapColumns = columns;
        _bitmapRows = rows;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private unsafe void DrawPixelField(double elapsed, double reveal, double frontier,
        int columns, int rows, double cell, double width, double pixelOpacity, double drain)
    {
        var hasDrain = !double.IsNaN(drain);

        var leftColor = UltraCodePalette.LeftColor;
        var highlightColor = UltraCodePalette.HighlightColor;
        var peakColor = UltraCodePalette.PeakColor;
        var tones = UltraCodePalette.Tones;
        var toneFloor = UltraCodePalette.ToneFloor;
        var toneCeiling = UltraCodePalette.ToneCeiling;

        using var framebuffer = _bitmap!.Lock();
        var buffer = (byte*)framebuffer.Address;
        var stride = framebuffer.RowBytes;

        // 清空整张位图，未点亮的格子保持透明。
        for (var y = 0; y < rows * SubSamples; y++)
        {
            Unsafe.InitBlockUnaligned(buffer + y * stride, 0, (uint)(columns * SubSamples * 4));
        }

        // 流动周期跟着「流动速度」倍率走：倍率越大，周期越短，流得越快。
        var flowDuration = BaseFlowDurationMs / Math.Clamp(UltraCodeTuning.Current.FlowSpeed, 0.1, 5.0);
        var rawFlow = elapsed / flowDuration;
        var flowCycle = Math.Floor(rawFlow);
        var easedFlow = flowCycle + Smoothstep(0, 1, rawFlow - flowCycle);

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var normalizedX = (column * cell + cell * 0.5) / width;
                var revealAlpha = Smoothstep(frontier - 0.1, frontier + 0.07, normalizedX);
                if (revealAlpha <= 0.002)
                {
                    continue;
                }

                // 倒计时：收边右侧整列熄灭，边界处留一圈高斯衰减的高光。
                // 这样「还剩多少」有三个同时可读的通道——彩带长度、前沿位置、前沿亮度。
                var burn = 0.0;
                if (hasDrain)
                {
                    var drainAlpha = Smoothstep(drain + 0.012, drain - 0.028, normalizedX);
                    if (drainAlpha <= 0.002)
                    {
                        continue;
                    }

                    revealAlpha *= drainAlpha;
                    var burnDistance = (normalizedX - drain) / BurnWidth;
                    burn = Math.Exp(-burnDistance * burnDistance);
                }

                // 这一项决定「从左端雾色混到主色」的进度。原来从 0.1 才起步，
                // 导致左边一大截几乎全是雾色（近中性灰）。往左挪并压缩区间，
                // 让主色从最左边就开始参与，整条带子都有颜色。
                var purpleAmount = Mix(0.28, 1.0, Smoothstep(0.0, 0.62, normalizedX));
                // 左端的像素密度。原来要到 38% 才铺满，左边一大截几乎是空的，
                // 加上底色本来就淡，看起来就是「没上色」。提前铺开。
                var fieldIntensity = Mix(0.45, 1.0, Smoothstep(0.0, 0.20, normalizedX));
                var depthBias = Smoothstep(0.35, 0.95, normalizedX);

                var baseHash = Hash(column * 12.9898 + row * 78.233, 43758.5453);
                var tempoHash = Hash(column * 7.13 + row * 19.41, 19341.731);
                var phaseHash = Hash(column * 31.17 + row * 11.93, 28437.123);
                var chromaHash = Hash(column * 9.47 + row * 67.13, 15823.917);

                var period = 500 + tempoHash * 1500;
                var localTime = elapsed + phaseHash * period;
                var cycle = Math.Floor(localTime / period);
                var cycleProgress = localTime % period / period;
                var cycleHash = Hash(column * 17.17 + row * 41.73 + cycle * 13.11, 24634.6345);
                var widthHash = Hash(column * 5.37 + row * 29.11 + cycle * 7.43, 17391.443);

                var pulseCenter = 0.2 + cycleHash * 0.55;
                var pulseWidth = 0.09 + widthHash * 0.08;
                var pulseDistance = (cycleProgress - pulseCenter) / pulseWidth;
                var pulseEnvelope = Math.Exp(-pulseDistance * pulseDistance * 1.45);
                var activeCycle = cycleHash > 0.12 ? 1.0 : 0.26;
                var irregularFlicker = pulseEnvelope * activeCycle;

                var flowCoordinate = (normalizedX + easedFlow) * 9;
                var flowIndex = Math.Floor(flowCoordinate);
                var flowProgress = Smoothstep(0, 1, flowCoordinate - flowIndex);
                var flowHashA = Hash(flowIndex * 18.31 + row * 37.17, 19283.173);
                var flowHashB = Hash((flowIndex + 1) * 18.31 + row * 37.17, 19283.173);
                var clusterGate = Smoothstep(0.46, 0.84, Mix(flowHashA, flowHashB, flowProgress));
                var wavePhase = (normalizedX + easedFlow + row * 0.06 + baseHash * 0.02) * Math.PI * 2;
                var directionalWave = Math.Pow(0.5 + 0.5 * Math.Cos(wavePhase), 5);
                var directionalFlow = Math.Max(clusterGate, directionalWave * 0.62);
                var flowingFlicker = Math.Max(
                    irregularFlicker * (0.48 + directionalFlow * 0.58),
                    directionalFlow * (0.38 + baseHash * 0.28));

                var revealGlow = reveal < 0.995
                    ? Math.Exp(-((normalizedX - frontier) * (normalizedX - frontier)) / 0.012)
                      * (1 - Smoothstep(0.7, 1, reveal))
                    : 0.0;
                // 燃烧前沿也算一路光源，且带上格子自身的哈希，让这圈高光是有颗粒的而不是一条实线。
                var burnGlow = burn * (0.55 + baseHash * 0.45);
                var lightAmount = Math.Max(
                    Math.Max(flowingFlicker, revealGlow * (0.4 + baseHash * 0.4)),
                    burnGlow);

                var peakHighlight = lightAmount > 0.4 && irregularFlicker > 0.16
                                                      && cycleHash > 0.26 && clusterGate > 0.04;
                var hottestHighlight = lightAmount > 0.68 && irregularFlicker > 0.3
                                                          && cycleHash > 0.48 && clusterGate > 0.12;
                var highlightAmount = peakHighlight
                    ? 0.97
                    : Math.Clamp(lightAmount * (0.44 + cycleHash * 0.3), 0, 0.64);
                highlightAmount = Math.Max(highlightAmount, burnGlow * 0.85);

                var toneDrift = baseHash * 0.28
                                + depthBias * 0.28
                                + cycleProgress * 0.38
                                + easedFlow * 0.18
                                + cycleHash * 0.2
                                + Math.Sin(elapsed * 0.00135 + phaseHash * Math.PI * 2) * 0.14;
                var tonePosition = (toneDrift % 1 + 1) % 1 * tones.Length;
                var toneIndex = (int)Math.Floor(tonePosition);
                var toneMix = tonePosition - toneIndex;
                var toneA = tones[toneIndex];
                var toneB = tones[(toneIndex + 1) % tones.Length];

                var chromaNudge = (chromaHash - 0.5) * 10 + depthBias * 12;
                var variedR = Math.Clamp(Mix(toneA[0], toneB[0], toneMix) + chromaNudge * 0.35 - depthBias * 8,
                    toneFloor[0], toneCeiling[0]);
                var variedG = Math.Clamp(Mix(toneA[1], toneB[1], toneMix) - depthBias * 16 + (baseHash - 0.5) * 8,
                    toneFloor[1], toneCeiling[1]);
                var variedB = Math.Clamp(Mix(toneA[2], toneB[2], toneMix) + depthBias * 6 + (cycleHash - 0.5) * 6,
                    toneFloor[2], toneCeiling[2]);

                var baseR = Mix(leftColor[0], variedR, purpleAmount);
                var baseG = Mix(leftColor[1], variedG, purpleAmount);
                var baseB = Mix(leftColor[2], variedB, purpleAmount);

                double r, g, b;
                if (hottestHighlight)
                {
                    r = Mix(baseR, peakColor[0], 0.95);
                    g = Mix(baseG, peakColor[1], 0.95);
                    b = Mix(baseB, peakColor[2], 0.95);
                }
                else
                {
                    r = Mix(baseR, highlightColor[0], highlightAmount);
                    g = Mix(baseG, highlightColor[1], highlightAmount);
                    b = Mix(baseB, highlightColor[2], highlightAmount);
                }

                // 前沿最热的一小圈直接压到峰值色，让收边始终是整条里最亮的地方。
                if (burn > 0.2)
                {
                    var burnMix = Math.Clamp((burn - 0.2) * 1.05, 0, 0.8);
                    r = Mix(r, peakColor[0], burnMix);
                    g = Mix(g, peakColor[1], burnMix);
                    b = Mix(b, peakColor[2], burnMix);
                }

                var baseOpacity = 0.7 + baseHash * 0.2;
                var alpha = pixelOpacity * (peakHighlight || hottestHighlight
                    ? revealAlpha * fieldIntensity
                    : revealAlpha * fieldIntensity * Math.Clamp(baseOpacity + flowingFlicker * 0.12, 0, 1));
                // 前沿不受 PixelOpacity 压制：正文那层为了让字清楚会把像素调暗，
                // 但收边是要被一眼看到的，这里单独把它提回来。
                if (burn > 0.002)
                {
                    alpha = Math.Min(1.0, alpha + revealAlpha * fieldIntensity * burn * 0.75);
                }

                if (alpha <= 0.002)
                {
                    continue;
                }

                var a8 = (byte)Math.Clamp(Math.Round(alpha * 255), 0, 255);
                var pixel = Premultiply(a8, r, g, b);

                var originX = column * SubSamples;
                var originY = row * SubSamples;
                for (var sy = 0; sy < SubFill; sy++)
                {
                    var scanline = (uint*)(buffer + (originY + sy) * stride) + originX;
                    for (var sx = 0; sx < SubFill; sx++)
                    {
                        scanline[sx] = pixel;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 构造裁剪区两端的羽化遮罩。两个参数都是相对裁剪区宽度的比例，传 0 表示该侧不羽化。
    /// </summary>
    private static IBrush CreateEdgeFade(double leftFraction, double rightFraction)
    {
        var stops = new GradientStops();
        if (leftFraction > 0)
        {
            stops.Add(new GradientStop(Colors.Transparent, 0.0));
            stops.Add(new GradientStop(Colors.Black, leftFraction));
        }
        else
        {
            stops.Add(new GradientStop(Colors.Black, 0.0));
        }

        if (rightFraction > 0)
        {
            stops.Add(new GradientStop(Colors.Black, 1.0 - rightFraction));
            stops.Add(new GradientStop(Colors.Transparent, 1.0));
        }
        else
        {
            stops.Add(new GradientStop(Colors.Black, 1.0));
        }

        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops = stops
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Premultiply(byte a, double r, double g, double b)
    {
        var pr = (uint)Math.Clamp(Math.Round(r) * a / 255.0, 0, 255);
        var pg = (uint)Math.Clamp(Math.Round(g) * a / 255.0, 0, 255);
        var pb = (uint)Math.Clamp(Math.Round(b) * a / 255.0, 0, 255);
        return ((uint)a << 24) | (pr << 16) | (pg << 8) | pb;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Hash(double seed, double scale) => Math.Abs(Math.Sin(seed) * scale) % 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Mix(double from, double to, double amount) => from + (to - from) * amount;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Smoothstep(double edge0, double edge1, double value)
    {
        var x = Math.Clamp((value - edge0) / (edge1 - edge0), 0.0, 1.0);
        return x * x * (3 - 2 * x);
    }
}

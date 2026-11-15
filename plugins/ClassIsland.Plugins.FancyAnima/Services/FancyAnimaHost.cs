using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Services;
using FancyAnima.Overlay;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FancyAnima.Services;

/// <summary>
/// FancyAnima 宿主服务 —— 提供涟漪特效的播放入口与资源生命周期管理。
///
/// 架构：
/// - 触发完全由提醒管线驱动：特效风格选为涟漪时，NotificationWorkerService 经
///   INotificationRippleEffect 调用 PlayRippleAsync，本服务不自行监听提醒状态
/// - 渲染由 Overlay.FancyAnimaRippleOverlay 完成：GDI UpdateLayeredWindow 原生窗口，
///   独立线程渲染，完全脱离 Avalonia 渲染管线与 UI 线程
/// - 窗口池：渲染窗口只创建一次，动画结束移到屏幕外复用，避免资源累积卡顿
/// </summary>
public class FancyAnimaHost : IHostedService, INotificationRippleEffect
{
    private readonly Settings.Settings _settings;
    private readonly ILogger<FancyAnimaHost> _logger;

    // 宿主能力委托（经 BuiltInPluginConfig 注入）：主题主色兜底。
    private readonly Func<Color> _fallbackThemeColor;

    private FancyAnimaRippleOverlay? _overlay;
    private bool _isRipplePlaying;

    public FancyAnimaHost(
        Settings.Settings settings,
        ILogger<FancyAnimaHost> logger,
        Func<Color> fallbackThemeColor)
    {
        _settings = settings;
        _logger = logger;
        _fallbackThemeColor = fallbackThemeColor;
    }

    // ============================================================
    //  IHostedService
    // ============================================================

    public Task StartAsync(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("[FancyAnima] 涟漪特效待命（由提醒管线按特效风格触发）");

            // 预热涟漪窗口：启动时就创建好原生窗口 + 资源，
            // 提醒响起时直接播放，首播不再有"现建窗口"的卡顿。
            if (_settings.EnableHighPerformanceAnimation)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        _overlay ??= new FancyAnimaRippleOverlay(
                            _settings.FpsLimit, _settings.Quality);
                        // 渲染线程日志接入 ClassIsland 日志系统（Console 输出在宿主内会丢失）
                        _overlay.Log = msg => _logger.LogInformation(msg);
                        _overlay.Prewarm();
                        _logger.LogInformation("[FancyAnima] 涟漪窗口已预热");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[FancyAnima] 涟漪窗口预热失败（首播将走兜底路径）");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FancyAnima] 初始化失败");
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        // 真正销毁渲染窗口与资源（卸载无残留）
        _overlay?.Shutdown();
        _overlay = null;
        return Task.CompletedTask;
    }

    // ============================================================
    //  INotificationRippleEffect（提醒管线播放入口）
    // ============================================================

    /// <inheritdoc />
    public Task PlayRippleAsync()
    {
        // 动画期间重复触发直接忽略（750ms 动画 + 官方特效叠加场景防双播）
        if (_isRipplePlaying) return Task.CompletedTask;
        if (!_settings.EnableHighPerformanceAnimation) return Task.CompletedTask;

        _isRipplePlaying = true;
        _logger.LogInformation("[FancyAnima] 提醒开始播放 → 触发涟漪特效");

        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                var hwnd = FindMainWindowHwnd();
                if (hwnd == IntPtr.Zero)
                {
                    _logger.LogWarning("[FancyAnima] 未找到主窗口 HWND，跳过本次涟漪");
                    return;
                }

                // 窗口池：只创建一次，之后复用
                _overlay ??= new FancyAnimaRippleOverlay(
                    _settings.FpsLimit, _settings.Quality);

                // 主题色跟随 ClassIsland：每次播放前重新解析
                var color = ResolveThemeColor();

                // 播放（渲染在独立线程完成，不占用 UI 线程）
                await _overlay.PlayAsync(hwnd, color, _settings.FpsLimit, _settings.Quality);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FancyAnima] 涟漪特效异常");
            }
            finally
            {
                _isRipplePlaying = false;
            }
        });
        return Task.CompletedTask;
    }

    // ============================================================
    //  主窗口定位 & 主题色
    // ============================================================

    /// <summary>ClassIsland 主窗口的 HWND（用于定位涟漪中心与所在显示器）</summary>
    private static IntPtr FindMainWindowHwnd()
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
            {
                foreach (var w in lifetime.Windows)
                {
                    var typeName = w.GetType().Name;
                    if (typeName.Contains("Main") && !typeName.Contains("Overlay")
                        && !typeName.Contains("Settings") && !typeName.Contains("Guide"))
                    {
                        return w.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                    }
                }

                return lifetime.MainWindow?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            }
        }
        catch { }
        return IntPtr.Zero;
    }

    /// <summary>
    /// 解析当前主题主色 —— 跟随 ClassIsland，而不是固定颜色。
    ///
    /// ClassIsland 使用 FluentAvalonia 主题，主色（自定义强调色或系统强调色）
    /// 会注入到 FluentAvalonia 的 Accent* 系列资源里。官方 ThemeService 自己
    /// 就是从 "AccentFillColorSelectedTextBackgroundBrush" 读回主色的，
    /// 所以我们优先从这些资源取，取不到再退到宿主注入的主色，最后才是固定色。
    /// </summary>
    private Color ResolveThemeColor()
    {
        // ---- 1. 主题资源（FluentAvalonia 强调色 + 常见自定义键）----
        try
        {
            var app = Application.Current;
            if (app != null)
            {
                // 关键：必须用 TryFindResource（连样式资源一起搜）。
                // FluentAvalonia 的资源挂在主题样式里，Resources.TryGetResource 搜不到。
                // 官方 ThemeService 也是这么读的：TryFindResource(key, ThemeVariant.Default, ...)
                var variants = new ThemeVariant?[] { ThemeVariant.Default, app.RequestedThemeVariant, null };

                foreach (var key in new[]
                {
                    // FluentAvalonia 强调色资源 —— ClassIsland 主题主色的实际载体
                    "AccentFillColorSelectedTextBackgroundBrush",
                    "AccentFillColorDefaultBrush",
                    "AccentTextFillColorPrimaryBrush",
                    "SystemControlHighlightAccentBrush",
                    "SystemAccentColor",
                    // 通用自定义键（兜底）
                    "PrimaryColor", "AccentColor", "ThemePrimaryColor",
                    "PrimaryBrush", "AccentBrush"
                })
                {
                    foreach (var variant in variants)
                    {
                        if (app.TryFindResource(key, variant, out var value))
                        {
                            if (value is Color c && c.A > 0) return c;
                            if (value is SolidColorBrush b && b.Color.A > 0) return b.Color;
                        }
                    }
                }
            }
        }
        catch { }

        // ---- 2. 兜底：宿主注入的设置主色（Settings.PrimaryColor）----
        try
        {
            var c = _fallbackThemeColor();
            if (c.A > 0) return c;
        }
        catch { }

        // ---- 3. 最后兜底：固定颜色 ----
        return Color.FromRgb(120, 180, 255);
    }
}

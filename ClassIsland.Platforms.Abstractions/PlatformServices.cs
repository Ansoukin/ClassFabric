using ClassIsland.Platforms.Abstraction.Services;
using ClassIsland.Platforms.Abstraction.Stubs.Services;

namespace ClassIsland.Platforms.Abstraction;

/// <summary>
/// 用于获取各个平台服务的基础类。
/// </summary>
public static class PlatformServices
{
    /// <summary>
    /// 平台窗口服务
    /// </summary>
    public static IWindowPlatformService WindowPlatformService { get; internal set; } = new WindowPlatformServiceStub();

    /// <summary>
    /// 平台定位服务
    /// </summary>
    public static ILocationService LocationService { get; internal set; } = new LocationServiceStub();

    /// <summary>
    /// 平台触觉反馈服务
    /// </summary>
    public static IHapticFeedbackService HapticFeedbackService { get; internal set; } = new HapticFeedbackServiceStub();
    
    /// <summary>
    /// 平台桌面服务
    /// </summary>
    public static IDesktopService DesktopService { get; internal set; } = new DesktopServiceStub();

    /// <summary>
    /// 当前平台是否支持定位服务
    /// </summary>
    public static bool IsLocationSupported => LocationService is not LocationServiceStub;

    /// <summary>
    /// 系统事件注册服务
    /// </summary>
    public static ISystemEventsService SystemEventsService { get; internal set; } = new SystemEventsServiceStub();

    /// <summary>
    /// 桌面通知服务
    /// </summary>
    public static IDesktopToastService DesktopToastService { get; internal set; } = new DesktopToastServiceStub();
    
    /// <summary>
    /// 平台文件选取服务
    /// </summary>
    public static IPlatformFilePickerService FilePickerService { get; internal set; } = new AvaloniaDefaultPlatformFilePickerService();

    internal static string BlobRootFolderPath { get; set; } = Path.Combine(".", "Blobs");

    /// <summary>
    /// 应用生命周期操作服务
    /// </summary>
    internal static IAppLifetimeService AppLifetimeService { get; set; } = new AppLifetimeServiceStub();

    /// <summary>
    /// 启动器服务
    /// </summary>
    public static ILauncherService LauncherService { get; internal set; } = new LauncherServiceStub();

    /// <summary>
    /// 系统键鼠模拟与输入空闲查询服务
    /// </summary>
    public static ISystemInputService SystemInputService { get; internal set; } = new SystemInputServiceStub();

    /// <summary>
    /// 系统环境调整服务（壁纸/主题/强调色/音量/截图/前台窗口）
    /// </summary>
    public static ISystemEnvironmentService SystemEnvironmentService { get; internal set; } =
        new SystemEnvironmentServiceStub();

    /// <summary>
    /// 系统电源操作服务（立即关机/重启）
    /// </summary>
    public static ISystemPowerService SystemPowerService { get; internal set; } = new SystemPowerServiceStub();

    /// <summary>
    /// 系统全局热键服务
    /// </summary>
    public static ISystemHotkeyService SystemHotkeyService { get; internal set; } = new SystemHotkeyServiceStub();
}

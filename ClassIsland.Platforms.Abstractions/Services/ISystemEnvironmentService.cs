namespace ClassIsland.Platforms.Abstraction.Services;

/// <summary>
/// 前台窗口操作种类。
/// </summary>
public enum ForegroundWindowOperation
{
    /// <summary>最大化。</summary>
    Maximize,
    /// <summary>最小化。</summary>
    Minimize,
    /// <summary>还原。</summary>
    Restore,
    /// <summary>关闭。</summary>
    Close
}

/// <summary>
/// 一个 RGB 颜色，供强调色等系统外观接口使用。
/// </summary>
public readonly record struct AccentColorRgb(byte R, byte G, byte B);

/// <summary>
/// 系统强调色快照。由平台实现方创建和填充，应用层只透传给 <see cref="ISystemEnvironmentService.RestoreAccentColor"/>。
/// </summary>
public sealed class AccentColorSnapshot
{
    /// <summary>
    /// 实现方自由使用的载荷（注册表值、未公开接口的内存偏好等）。
    /// </summary>
    public Dictionary<string, object?> Data { get; } = new();
}

/// <summary>
/// 系统环境调整：壁纸、主题、强调色、音量、截图与前台窗口操作。
/// 仅 Windows 桌面端有真实现，其余平台为空操作 stub 并返回 false。
/// </summary>
public interface ISystemEnvironmentService
{
    /// <summary>
    /// 设置图片壁纸。<paramref name="fitStyle"/> 与行动设置里的契合度枚举一致：
    /// 0=平铺 1=居中 2=拉伸 3=填充 4=适应 5=跨区。
    /// </summary>
    bool SetWallpaperImage(string imagePath, int fitStyle);

    /// <summary>
    /// 设置纯色壁纸（写 Control Panel\Colors\Background 并清空壁纸图）。
    /// </summary>
    bool SetWallpaperSolidColor(byte r, byte g, byte b);

    /// <summary>
    /// 切换系统主题。true=浅色，false=深色。
    /// </summary>
    bool SetSystemTheme(bool light);

    /// <summary>
    /// 捕获当前系统强调色状态，供 <see cref="RestoreAccentColor"/> 还原。
    /// </summary>
    AccentColorSnapshot CaptureAccentColor();

    /// <summary>
    /// 应用系统强调色。只换强调色本身，不改「强调色应用范围」（任务栏/标题栏是否着色保持用户原设置）。
    /// </summary>
    bool ApplyAccentColor(AccentColorRgb color);

    /// <summary>
    /// 把系统强调色还原到快照时的状态。
    /// </summary>
    bool RestoreAccentColor(AccentColorSnapshot snapshot);

    /// <summary>
    /// 设置系统音量百分比 0-100。<paramref name="mute"/> 为 null 表示不变更静音状态。
    /// </summary>
    bool SetSystemVolume(float percent, bool? mute);

    /// <summary>
    /// 全屏截图并保存为 PNG。
    /// </summary>
    bool CaptureScreenToPng(string filePath);

    /// <summary>
    /// 对前台窗口执行最大化/最小化/还原/关闭。
    /// </summary>
    bool OperateForegroundWindow(ForegroundWindowOperation operation);
}

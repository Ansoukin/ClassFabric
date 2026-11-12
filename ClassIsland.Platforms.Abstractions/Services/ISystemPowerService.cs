namespace ClassIsland.Platforms.Abstraction.Services;

/// <summary>
/// 系统电源操作（立即关机/重启）。
/// 仅 Windows 桌面端有真实现，其余平台为空操作 stub 并返回 false。
/// 计时关机、锁屏、睡眠等走系统命令的能力不在此，由行动直接 Process.Start。
/// </summary>
public interface ISystemPowerService
{
    /// <summary>
    /// 立即关机。powerOff=true 时附带断电（EWX_POWEROFF），否则仅关机（EWX_SHUTDOWN）。
    /// </summary>
    bool Shutdown(bool powerOff);

    /// <summary>
    /// 立即重启。
    /// </summary>
    bool Restart();
}

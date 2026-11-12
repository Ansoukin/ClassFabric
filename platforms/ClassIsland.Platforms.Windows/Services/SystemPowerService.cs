using System.Runtime.InteropServices;
using ClassIsland.Platforms.Abstraction.Services;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.System.Shutdown;

namespace ClassIsland.Platform.Windows.Services;

/// <summary>
/// Windows 端立即关机/重启实现。
/// RtlAdjustPrivilege 是 ntdll 未公开函数，CsWin32 无法生成，只能手工 DllImport。
/// </summary>
public class SystemPowerService(ILogger<SystemPowerService>? logger = null) : ISystemPowerService
{
    const int SeShutdownPrivilege = 19;
    const int StatusSuccess = 0x00000000;

    /// <inheritdoc />
    public bool Shutdown(bool powerOff)
    {
        return Execute(() => PInvoke.ExitWindowsEx(
            EXIT_WINDOWS_FLAGS.EWX_SHUTDOWN | EXIT_WINDOWS_FLAGS.EWX_POWEROFF,
            SHUTDOWN_REASON.SHTDN_REASON_FLAG_PLANNED), powerOff ? "立即关机" : "立即关机(不断电)");
    }

    /// <inheritdoc />
    public bool Restart()
    {
        return Execute(() => PInvoke.ExitWindowsEx(
            EXIT_WINDOWS_FLAGS.EWX_REBOOT,
            SHUTDOWN_REASON.SHTDN_REASON_FLAG_PLANNED), "立即重启");
    }

    bool Execute(Func<bool> action, string label)
    {
        // ExitWindowsEx 需要 SeShutdownPrivilege，普通用户进程默认未启用，先提权。
        if (W32RtlAdjustPrivilege(SeShutdownPrivilege, true, false, out _) != StatusSuccess)
        {
            logger?.LogError("执行{Label}失败：获取 SeShutdownPrivilege 权限失败", label);
            return false;
        }

        if (!action())
        {
            logger?.LogError("执行{Label}失败：ExitWindowsEx 返回失败", label);
            return false;
        }

        logger.LogInformation("已执行{Label}命令", label);
        return true;
    }

    [DllImport("ntdll.dll", EntryPoint = "RtlAdjustPrivilege")]
    static extern uint W32RtlAdjustPrivilege(int privilege, bool enablePrivilege, bool isThreadPrivilege,
        out bool previousValue);
}

using System;
using System.Diagnostics;

namespace ClassFabric.Services.Automation.PlatformTools;

/// <summary>
/// 电源操作统一入口。计划类任务复用系统 shutdown.exe（自带权限与倒计时语义），
/// 即时锁屏与睡眠走系统 API。
/// </summary>
public class PowerService
{
    public virtual void ScheduleShutdown(int seconds)
    {
        RunShutdownTool($"-s -t {Math.Max(seconds, 0)}");
    }

    public virtual void CancelShutdown()
    {
        RunShutdownTool("-a");
    }

    public virtual void Restart()
    {
        RunShutdownTool("-r -t 0");
    }

    public virtual void PowerOff()
    {
        RunShutdownTool("-s -t 0");
    }

    public virtual void LockScreen()
    {
        _ = NativeInterop.LockWorkStation();
    }

    public virtual void Sleep()
    {
        // Hibernate=false 走普通睡眠，Force=false 给应用拦截休眠事件的机会
        _ = NativeInterop.SetSuspendState(false, false, false);
    }

    private static void RunShutdownTool(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "shutdown.exe",
            Arguments = arguments,
            UseShellExecute = true,
            CreateNoWindow = true,
        };
        using var process = Process.Start(startInfo);
    }
}

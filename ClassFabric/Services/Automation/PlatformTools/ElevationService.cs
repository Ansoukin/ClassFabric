using System;
using System.Diagnostics;

namespace ClassFabric.Services.Automation.PlatformTools;

/// <summary>
/// 提权操作统一入口：UAC 弹窗式提权，禁止落盘任何脚本文件。
/// </summary>
public class ElevationService
{
    /// <summary>
    /// 以管理员身份运行指定程序并等待退出，返回退出码。用户拒绝 UAC 时返回 null。
    /// </summary>
    public virtual int? RunElevated(string fileName, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = true,
            Verb = "runas",
            CreateNoWindow = true,
        };
        try
        {
            using var process = Process.Start(startInfo);
            if (process == null) return null;
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // 用户在 UAC 弹窗点了取消
            return null;
        }
    }

    /// <summary>
    /// 启用/禁用一个即插即用设备。经 pnputil 参数化调用，不生成脚本。
    /// </summary>
    public virtual bool SetDeviceState(string deviceId, bool enable)
    {
        var arg = enable ? $"/enable-device \"{deviceId}\"" : $"/disable-device \"{deviceId}\"";
        return RunElevated("pnputil.exe", arg) == 0;
    }

    /// <summary>
    /// 以管理员身份重启当前应用。
    /// </summary>
    public virtual void RestartSelfAsAdmin()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe)) return;
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = true,
            Verb = "runas",
        });
    }
}

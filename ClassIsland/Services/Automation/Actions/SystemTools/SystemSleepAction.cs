using System;
using System.Diagnostics;
using System.Threading.Tasks;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Icons;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services.Automation.Actions.SystemTools;

/// <summary>
/// 系统睡眠。调 rundll32 的 SetSuspendState，仅 Windows 有效。
/// 注意类名不能叫 SleepAction：主工程已有的 SleepAction 是「等待时长」行动，
/// 命名空间不同仍会与 using 同名冲突，这里按约定改名 SystemSleepAction，行动 ID 保持 SystemTools.Sleep 不变。
/// </summary>
[AutomationCategory("电源选项")]
[ActionInfo("SystemTools.Sleep", "系统睡眠", FluentIcons.WeatherMoonRegular, false)]
public class SystemSleepAction(ILogger<SystemSleepAction> logger) : ActionBase
{
    private readonly ILogger<SystemSleepAction> _logger = logger;

    protected override async Task OnInvoke()
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogWarning("SetSuspendState 仅支持 Windows，跳过系统睡眠。");
            await base.OnInvoke();
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "rundll32.exe",
                Arguments = "powrprof.dll,SetSuspendState 0,1,0",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(psi);
            _logger.LogInformation("已执行睡眠命令");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行睡眠失败");
            throw;
        }

        await base.OnInvoke();
    }
}

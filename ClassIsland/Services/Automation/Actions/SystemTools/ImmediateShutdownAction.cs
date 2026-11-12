using System;
using System.Threading.Tasks;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Icons;
using ClassIsland.Platforms.Abstraction;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services.Automation.Actions.SystemTools;

/// <summary>
/// 立即关机。插件版手搓 RtlAdjustPrivilege + ExitWindowsEx（EWX_SHUTDOWN|EWX_POWEROFF），
/// 主程序已收敛到 SystemPowerService，powerOff=true 对应断电语义。
/// </summary>
[AutomationCategory("电源选项")]
[ActionInfo("SystemTools.ImmediateShutdown", "立即关机", FluentIcons.PowerRegular, false)]
public class ImmediateShutdownAction(ILogger<ImmediateShutdownAction> logger) : ActionBase
{
    private readonly ILogger<ImmediateShutdownAction> _logger = logger;

    protected override async Task OnInvoke()
    {
        if (!PlatformServices.SystemPowerService.Shutdown(true))
        {
            _logger.LogError("执行立即关机失败");
            throw new InvalidOperationException("执行立即关机失败");
        }

        _logger.LogInformation("已执行立即关机命令");
        await base.OnInvoke();
    }
}

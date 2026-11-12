using System;
using System.Threading.Tasks;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Icons;
using ClassIsland.Platforms.Abstraction;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services.Automation.Actions.SystemTools;

/// <summary>
/// 立即重启。插件版手搓 RtlAdjustPrivilege + ExitWindowsEx，主程序已收敛到
/// SystemPowerService（Windows 有真实现，其余平台返回 false），直接调它即可。
/// </summary>
[AutomationCategory("电源选项")]
[ActionInfo("SystemTools.ImmediateRestart", "立即重启", FluentIcons.ArrowCounterclockwiseRegular, false)]
public class ImmediateRestartAction(ILogger<ImmediateRestartAction> logger) : ActionBase
{
    private readonly ILogger<ImmediateRestartAction> _logger = logger;

    protected override async Task OnInvoke()
    {
        if (!PlatformServices.SystemPowerService.Restart())
        {
            _logger.LogError("执行立即重启失败");
            throw new InvalidOperationException("执行立即重启失败");
        }

        _logger.LogInformation("已执行立即重启命令");
        await base.OnInvoke();
    }
}

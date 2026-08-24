using System;
using System.Threading.Tasks;
using ClassFabric.Core.Abstractions.Automation;
using ClassFabric.Core.Attributes;
using ClassFabric.Models.Actions;
using ClassFabric.Services.Automation.PlatformTools;
using ClassFabric.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace ClassFabric.Services.Automation.Actions;

[ActionInfo("classfabric.action.triggerCustomTrigger", "触发指定触发器", "\uEA3F")]
public class TriggerCustomTriggerAction : ActionBase<TriggerCustomTriggerActionSettings>
{
    ActionFlowCoordinatorService Coordinator =>
        IAppHost.Host.Services.GetRequiredService<ActionFlowCoordinatorService>();

    protected override async Task OnInvoke()
    {
        await base.OnInvoke();
        if (Settings.TargetTriggerId == Guid.Empty) return;

        Coordinator.RequestFlow(Settings.TargetTriggerId);
    }
}

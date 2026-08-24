using System;
using ClassFabric.Core.Abstractions.Automation;
using ClassFabric.Core.Attributes;
using ClassFabric.Models.Automation.Triggers;

namespace ClassFabric.Services.Automation.Triggers;

[TriggerInfo("classfabric.trigger.actionInProgress", "收到指定行动流请求时", "\uEA3F")]
public class ActionInProgressTrigger(ActionInProgressTriggerHandlerService handlerService) : TriggerBase<ActionInProgressTriggerSettings>
{
    public ActionInProgressTriggerHandlerService HandlerService { get; } = handlerService;

    public override void Loaded()
    {
        HandlerService.Handled += HandlerServiceOnHandled;
    }

    public override void UnLoaded()
    {
        HandlerService.Handled -= HandlerServiceOnHandled;
    }

    private void HandlerServiceOnHandled(object? sender, Guid triggerId)
    {
        if (triggerId != Settings.TargetTriggerId) return;

        Trigger();
    }
}

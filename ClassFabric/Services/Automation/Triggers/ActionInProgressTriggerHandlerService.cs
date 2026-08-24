using System;
using ClassFabric.Core.Abstractions.Services;
using ClassFabric.Services.Automation.PlatformTools;

namespace ClassFabric.Services.Automation.Triggers;

public class ActionInProgressTriggerHandlerService
{
    public event EventHandler<Guid>? Handled;

    public ActionInProgressTriggerHandlerService(IActionService actionService, ActionFlowCoordinatorService coordinator)
    {
        coordinator.FlowRequested.Subscribe(triggerId => Handled?.Invoke(this, triggerId));
    }
}

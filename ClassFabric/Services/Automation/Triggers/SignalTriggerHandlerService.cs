using System;
using ClassFabric.Core.Abstractions.Services;
using ClassFabric.Models.Automation.Triggers;
using ClassFabric.Models.EventArgs;

namespace ClassFabric.Services.Automation.Triggers;

public class SignalTriggerHandlerService
{
    public event EventHandler<SignalTriggerEventArgs>? Handled;

    public void EmitSignal(string name, bool revert)
    {
        Handled?.Invoke(this, new SignalTriggerEventArgs(name, revert));
    }

    public SignalTriggerHandlerService(IActionService actionService)
    {
        actionService.RegisterActionHandler("classfabric.broadcastSignal", (o, guid) =>
        {
            if (o is SignalTriggerSettings settings)
            {
                EmitSignal(settings.SignalName, settings.IsRevert);
            }
        });
        actionService.RegisterRevertHandler("classfabric.broadcastSignal", (o, guid) =>
        {
            if (o is SignalTriggerSettings settings)
            {
                EmitSignal(settings.SignalName, !settings.IsRevert);
            }
        });
    }
}
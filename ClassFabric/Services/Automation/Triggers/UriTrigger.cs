using ClassFabric.Core.Abstractions.Automation;
using ClassFabric.Core.Attributes;
using ClassFabric.Models.Automation.Triggers;
using ClassFabric.Models.EventArgs;

namespace ClassFabric.Services.Automation.Triggers;

[TriggerInfo("classfabric.uri", "调用 Uri 时", "\ueab0")]
public class UriTrigger(UriTriggerHandlerService uriTriggerHandlerService) : TriggerBase<UriTriggerSettings>
{
    private UriTriggerHandlerService UriTriggerHandlerService { get; } = uriTriggerHandlerService;

    public override void Loaded()
    {
        UriTriggerHandlerService.HandledRun += UriTriggerHandlerServiceOnHandledRun;
        UriTriggerHandlerService.HandledRevert += UriTriggerHandlerServiceOnHandledRevert;
    }

    private void UriTriggerHandlerServiceOnHandledRevert(object? sender, UriTriggerHandledEventArgs e)
    {
        if (e.Name == Settings.UriSuffix)
        {
            TriggerRevert();
        }
    }

    private void UriTriggerHandlerServiceOnHandledRun(object? sender, UriTriggerHandledEventArgs e)
    {
        if (e.Name == Settings.UriSuffix)
        {
            Trigger();
        }
    }

    public override void UnLoaded()
    {
        UriTriggerHandlerService.HandledRun -= UriTriggerHandlerServiceOnHandledRun;
        UriTriggerHandlerService.HandledRevert -= UriTriggerHandlerServiceOnHandledRevert;
    }
}
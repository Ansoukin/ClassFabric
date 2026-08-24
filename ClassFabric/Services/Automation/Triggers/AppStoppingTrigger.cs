using System;
using ClassFabric.Core;
using ClassFabric.Core.Abstractions.Automation;
using ClassFabric.Core.Attributes;

namespace ClassFabric.Services.Automation.Triggers;

[TriggerInfo("classfabric.lifetime.stopping", "应用退出时", "\ue0df")]
public class AppStoppingTrigger : TriggerBase
{
    public override void Loaded()
    {
        AppBase.Current.AppStopping += CurrentOnAppStarted;
    }

    public override void UnLoaded()
    {
        AppBase.Current.AppStopping -= CurrentOnAppStarted;
    }

    private void CurrentOnAppStarted(object? sender, EventArgs e)
    {
        Trigger();
    }
}
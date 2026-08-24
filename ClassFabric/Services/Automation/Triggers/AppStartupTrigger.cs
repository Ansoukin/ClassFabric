using System.Diagnostics;
using System.Linq;
using ClassFabric.Core;
using ClassFabric.Core.Abstractions.Automation;
using ClassFabric.Core.Attributes;
using ClassFabric.Core.Enums;

namespace ClassFabric.Services.Automation.Triggers;

[TriggerInfo("classfabric.lifetime.startup", "应用启动时", "\ue067")]
public class AppStartupTrigger : TriggerBase
{
    public override void Loaded()
    {
        if (AppBase.CurrentLifetime < ApplicationLifetime.Running) {
            Trigger();
        }
    }

    public override void UnLoaded()
    {
    }
}
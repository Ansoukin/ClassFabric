using ClassFabric.Core.Abstractions.Controls;
using ClassFabric.Models.Automation.Triggers;

namespace ClassFabric.Controls.TriggerSettingsControls;

/// <summary>
/// SignalTriggerSettingsControl.axaml 的交互逻辑
/// </summary>
public partial class SignalTriggerSettingsControl : TriggerSettingsControlBase<SignalTriggerSettings>
{
    public SignalTriggerSettingsControl()
    {
        InitializeComponent();
    }
}

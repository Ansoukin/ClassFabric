using ClassFabric.Core.Abstractions.Controls;
using ClassFabric.Models.Automation.Triggers;

namespace ClassFabric.Controls.ActionSettingsControls;

/// <summary>
/// BroadcastSignalActionSettingsControl.xaml 的交互逻辑
/// </summary>
public partial class BroadcastSignalActionSettingsControl : ActionSettingsControlBase<SignalTriggerSettings>
{
    public BroadcastSignalActionSettingsControl()
    {
        InitializeComponent();
    }
}

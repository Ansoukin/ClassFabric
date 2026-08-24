using ClassFabric.Core.Abstractions.Controls;
using ClassFabric.Models.Automation.Triggers;

namespace ClassFabric.Controls.TriggerSettingsControls;

/// <summary>
/// CronTriggerSettingsControl.axaml 的交互逻辑
/// </summary>
public partial class CronTriggerSettingsControl : TriggerSettingsControlBase<CronTriggerSettings>
{
    public CronTriggerSettingsControl()
    {
        InitializeComponent();
    }
}
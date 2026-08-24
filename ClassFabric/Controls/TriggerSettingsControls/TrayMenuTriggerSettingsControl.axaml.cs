using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ClassFabric.Core.Abstractions.Controls;
using ClassFabric.Models.Automation.Triggers;

namespace ClassFabric.Controls.TriggerSettingsControls;

public partial class TrayMenuTriggerSettingsControl : TriggerSettingsControlBase<TrayMenuTriggerSettings>
{
    public TrayMenuTriggerSettingsControl()
    {
        InitializeComponent();
    }
}
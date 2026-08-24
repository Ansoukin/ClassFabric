using ClassFabric.Core.Abstractions.Controls;
using ClassFabric.Models.Actions;

namespace ClassFabric.Controls.ActionSettingsControls;

public partial class FlowConfirmationActionSettingsControl : ActionSettingsControlBase<FlowConfirmationActionSettings>
{
    public FlowConfirmationActionSettingsControl()
    {
        InitializeComponent();
        DataContext = this;
    }
}

using ClassFabric.Core.Abstractions.Controls;
using ClassFabric.Models.Actions;
namespace ClassFabric.Controls.ActionSettingsControls;

public partial class AppRestartActionSettingsControl : ActionSettingsControlBase<AppRestartActionSettings>
{
    public AppRestartActionSettingsControl()
    {
        InitializeComponent();
        DataContext = this;
    }
}

using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.Models.Automation.Triggers;

public partial class TrayMenuTriggerSettings : ObservableObject
{
    [ObservableProperty] private string _header = "";
    [ObservableProperty] private bool _isRevert = false;
}
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.Models.Automation.Triggers;

public partial class UriTriggerSettings : ObservableObject
{
    [ObservableProperty] private string _uriSuffix = "";
}
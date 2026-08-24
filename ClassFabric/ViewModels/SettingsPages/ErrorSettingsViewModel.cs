using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.ViewModels.SettingsPages;

public partial class ErrorSettingsViewModel : ObservableRecipient
{
    [ObservableProperty] private bool _isError = false;
}
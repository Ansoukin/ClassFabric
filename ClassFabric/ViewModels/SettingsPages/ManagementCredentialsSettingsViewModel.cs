using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.ViewModels.SettingsPages;

public partial class ManagementCredentialsSettingsViewModel : ObservableObject
{
    [ObservableProperty] private bool _isLocked = true;
}
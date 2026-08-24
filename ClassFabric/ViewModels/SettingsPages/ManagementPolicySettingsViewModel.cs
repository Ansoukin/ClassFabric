using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.ViewModels.SettingsPages;

public partial class ManagementPolicySettingsViewModel : ObservableObject
{
    [ObservableProperty] private bool _isLocked = true;
}
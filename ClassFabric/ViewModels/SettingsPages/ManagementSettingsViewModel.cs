using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.ViewModels.SettingsPages;

public partial class ManagementSettingsViewModel : ObservableObject
{
    [ObservableProperty] private Geometry? _cuidQrCodePath;
}
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.ViewModels;

public partial class RecoveryViewModel : ObservableObject
{
    [ObservableProperty] private bool _canGoBack;
}
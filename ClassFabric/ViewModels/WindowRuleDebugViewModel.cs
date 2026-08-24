using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.ViewModels;

public partial class WindowRuleDebugViewModel : ObservableObject
{
    [ObservableProperty] private string _foregroundWindowTitle = "？？？";

    [ObservableProperty] private string _foregroundWindowProcessName = "？？？";

    [ObservableProperty] private string _foregroundWindowClassName = "？？？";

    [ObservableProperty] private string _foregroundWindowState = "？？？";

    [ObservableProperty] private string _foregroundWindowHandle = "？？？";
}
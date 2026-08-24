using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.Models.Rules;

public partial class RainTimeRuleSettings : ObservableObject
{
    [ObservableProperty] private double _rainTimeMinutes = 60;

    [ObservableProperty] private bool _isRemainingTime = false;
}
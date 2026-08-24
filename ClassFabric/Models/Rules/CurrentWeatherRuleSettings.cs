using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.Models.Rules;

public partial class CurrentWeatherRuleSettings : ObservableObject
{
    [ObservableProperty] private int _weatherId = 0;
    
    [ObservableProperty] private bool _isFuzzyMatch = false;
}
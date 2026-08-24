using ClassFabric.Core.Abstractions.Controls;
using ClassFabric.Core.Abstractions.Services;
using ClassFabric.Models.Rules;

namespace ClassFabric.Controls.RuleSettingsControls;

/// <summary>
/// CurrentWeatherRuleSettingsControl.xaml 的交互逻辑
/// </summary>
public partial class CurrentWeatherRuleSettingsControl : RuleSettingsControlBase<CurrentWeatherRuleSettings>
{
    public IWeatherService WeatherService { get; }

    public CurrentWeatherRuleSettingsControl(IWeatherService weatherService)
    {
        WeatherService = weatherService;
        InitializeComponent();
    }
}
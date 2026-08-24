#if false
using System.Windows.Controls;

using ClassFabric.Models;
using ClassFabric.Services;

namespace ClassFabric.Controls.MiniInfoProvider;

/// <summary>
/// WeatherMiniInfoProviderControl.xaml 的交互逻辑
/// </summary>
public partial class WeatherMiniInfoProviderControl : UserControl
{
    private SettingsService SettingsService { get; }

    public Settings AppSettings => SettingsService.Settings;

    public WeatherMiniInfoProviderSettings Settings { get; }

    public WeatherMiniInfoProviderControl(SettingsService settingsService, WeatherMiniInfoProviderSettings weatherMiniInfoProviderSettings)
    {
        Settings = weatherMiniInfoProviderSettings;
        SettingsService = settingsService;
        InitializeComponent();
    }
}
#endif

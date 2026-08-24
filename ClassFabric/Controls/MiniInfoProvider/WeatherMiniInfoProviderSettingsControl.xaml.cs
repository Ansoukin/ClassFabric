#if false
using System.Windows.Controls;

using ClassFabric.Models;

namespace ClassFabric.Controls.MiniInfoProvider;

/// <summary>
/// WeatherMiniInfoProviderSettingsControl.xaml 的交互逻辑
/// </summary>
public partial class WeatherMiniInfoProviderSettingsControl : UserControl
{
    public WeatherMiniInfoProviderSettings Settings { get; }

    public WeatherMiniInfoProviderSettingsControl(WeatherMiniInfoProviderSettings settings)
    {
        Settings = settings;
        InitializeComponent();
    }
}
#endif

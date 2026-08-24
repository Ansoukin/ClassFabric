using System;
using System.Linq;
using System.Windows;
using Avalonia.Interactivity;
using ClassFabric.Core.Abstractions.Controls;
using ClassFabric.Core.Abstractions.Services;
using ClassFabric.Core.Controls;
using ClassFabric.Models.NotificationProviderSettings;

namespace ClassFabric.Controls.NotificationProviders;

/// <summary>
/// WeatherNotificationProviderSettingsControl.xaml 的交互逻辑
/// </summary>
public partial class WeatherNotificationProviderSettingsControl : NotificationProviderControlBase<
    WeatherNotificationProviderSettings>
{

    public WeatherNotificationProviderSettingsControl()
    {
        InitializeComponent();
    }

    private void ButtonShowAttachedSettingsInfo_OnClick(object sender, RoutedEventArgs e)
    {
        SettingsPageBase.OpenDrawerCommand.Execute(new RootAttachedSettingsDependencyControl(IAttachedSettingsHostService.RegisteredControls.First(x => x.Guid == new Guid("7625DE96-38AA-4B71-B478-3F156DD9458D"))));
    }
}

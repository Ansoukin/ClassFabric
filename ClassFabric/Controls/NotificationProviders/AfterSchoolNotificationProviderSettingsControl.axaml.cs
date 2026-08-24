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
/// AfterSchoolNotificationProviderSettingsControl.xaml 的交互逻辑
/// </summary>
public partial class AfterSchoolNotificationProviderSettingsControl : NotificationProviderControlBase<AfterSchoolNotificationProviderSettings>
{
    public AfterSchoolNotificationProviderSettingsControl()
    {
        InitializeComponent();
    }

    private void ButtonShowAttachedSettingsInfo_OnClick(object sender, RoutedEventArgs e)
    {
        SettingsPageBase.OpenDrawerCommand.Execute(new RootAttachedSettingsDependencyControl(IAttachedSettingsHostService.RegisteredControls.First(x => x.Guid == new Guid("8FBC3A26-6D20-44DD-B895-B9411E3DDC51"))));
    }
}

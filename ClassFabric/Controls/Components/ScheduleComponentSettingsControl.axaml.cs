using System;
using System.Linq;
using System.Windows;
using Avalonia.Interactivity;
using ClassFabric.Core.Abstractions.Controls;
using ClassFabric.Core.Abstractions.Services;
using ClassFabric.Core.Controls;
using ClassFabric.Models.ComponentSettings;
using ClassFabric.Services;

namespace ClassFabric.Controls.Components;

/// <summary>
/// ScheduleComponentSettingsControl.xaml 的交互逻辑
/// </summary>
public partial class ScheduleComponentSettingsControl : ComponentBase<LessonControlSettings>
{
    public SettingsService SettingsService { get; }

    public ScheduleComponentSettingsControl(SettingsService settingsService)
    {
        SettingsService = settingsService;
        InitializeComponent();
    }

    private void ButtonShowAttachedSettings_OnClick(object sender, RoutedEventArgs e)
    {
        SettingsPageBase.OpenDrawerCommand.Execute(new RootAttachedSettingsDependencyControl(IAttachedSettingsHostService.RegisteredControls.First(x => x.Guid == new Guid("58e5b69a-764a-472b-bcf7-003b6a8c7fdf"))));
    }
}

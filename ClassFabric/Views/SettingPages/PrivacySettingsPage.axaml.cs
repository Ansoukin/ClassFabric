using System;
using System.ComponentModel;
using System.Windows;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ClassFabric.Core.Abstractions.Controls;
using ClassFabric.Core.Attributes;
using ClassFabric.Core.Controls;
using ClassFabric.Core.Enums.SettingsWindow;
using ClassFabric.Services;

namespace ClassFabric.Views.SettingPages;

/// <summary>
/// PrivacySettingsPage.xaml 的交互逻辑
/// </summary>
[Group("classfabric.general")]
[SettingsPageInfo("privacy", "隐私", "\uef65", "\uef64", SettingsPageCategory.Internal)]
public partial class PrivacySettingsPage : SettingsPageBase
{
    public SettingsService SettingsService { get; }

    public PrivacySettingsPage(SettingsService settingsService)
    {
        InitializeComponent();
        DataContext = this;
        SettingsService = settingsService;
    }

    private void HyperlinkMsAppCenter_OnClick(object sender, RoutedEventArgs e)
    {
        new DocumentReaderWindow()
        {
            Source = new Uri("avares://ClassFabric/Assets/Documents/Privacy_.md"),
            Title = "ClassFabric 隐私政策"
        }.ShowDialog((TopLevel.GetTopLevel(this) as Window)!);
    }
}


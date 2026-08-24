using ClassFabric.Core.Abstractions.Controls;
using System;
using System.Windows;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ClassFabric.Core.Attributes;
using ClassFabric.Services;
using ClassFabric.ViewModels.SettingsPages;
using ClassFabric.Core.Enums.SettingsWindow;
using ClassFabric.Shared;

namespace ClassFabric.Views.SettingPages;

/// <summary>
/// AppearanceSettingsPage.xaml 的交互逻辑
/// </summary>
[Group("classfabric.mainwindow")]
[SettingsPageInfo("appearance", "外观", "\ue51e", "\ue51d", SettingsPageCategory.Internal)]
public partial class AppearanceSettingsPage : SettingsPageBase
{
    public AppearanceSettingsViewModel ViewModel { get; } = IAppHost.GetService<AppearanceSettingsViewModel>();

    public AppearanceSettingsPage()
    {
        InitializeComponent();
        DataContext = this;
    }

    private async void ButtonUpdateWallpaper_OnClick(object sender, SelectionChangedEventArgs e)
    {
        // if (SettingsService.Settings.ColorSource is 1 or 3)
        //     await WallpaperPickingService.GetWallpaperAsync();
    }

    private void ButtonPreviewWallpaper_OnClick(object sender, RoutedEventArgs e)
    {
        // var w = App.GetService<WallpaperPreviewWindow>();
        // w.Owner = Window.GetWindow(this);
        // w.ShowDialog();
    }

    private async void ButtonBrowseWindows_OnClick(object sender, RoutedEventArgs e)
    {
        // var w = new WindowsPicker(SettingsService.Settings.WallpaperClassName)
        // {
        //     Owner = Window.GetWindow(this),
        // };
        // var r = w.ShowDialog();
        // SettingsService.Settings.WallpaperClassName = w.SelectedResult ?? "";
        // if (r == true)
        // {
        //     await WallpaperPickingService.GetWallpaperAsync();
        // }
        // GC.Collect();
    }
}

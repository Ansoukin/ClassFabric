using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Controls.Ruleset;
using ClassIsland.Core.Enums.SettingsWindow;
using ClassIsland.Platforms.Abstraction;
using ClassIsland.Services;
using ClassIsland.Shared;
using ClassIsland.ViewModels.SettingsPages;

namespace ClassIsland.Views.SettingPages;

/// <summary>
/// GeneralSettingsPage.xaml 的交互逻辑
/// </summary>
[Group("classisland.general")]
[SettingsPageInfo("general", "基本", SettingsPageCategory.Internal)]
public partial class GeneralSettingsPage : SettingsPageBase
{
    public static List<FilePickerFileType> ImageFileTypes { get; } = [FilePickerFileTypes.ImageAll];

    public GeneralSettingsViewModel ViewModel { get; } = IAppHost.GetService<GeneralSettingsViewModel>();

    public GeneralSettingsPage()
    {
        InitializeComponent();
        DataContext = this;
        ((RulesetControl)this.FindResource("RulesetControl")).Ruleset = this.ViewModel.SettingsService.Settings.HideRules;
    }

    private void SettingsOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SettingsService.Settings.IsWaitForTransientDisabled) 
            or nameof(SettingsService.Settings.AnimationLevel)
            or nameof(SettingsService.Settings.LanguageCode))
        {
            RequestRestart();
        }
    }

    private void ButtonCloseMigrationTip_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SettingsService.Settings.ShowComponentsMigrateTip = false;
    }

    private void UriNavigationCommands_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is HyperlinkButton { CommandParameter: string url })
        {
            ClassIsland.Core.Commands.UriNavigationCommands.UriNavigationCommand.Execute(url);
        }
    }

    private void ButtonWeekOffsetSettingsButtons_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.IsWeekOffsetSettingsOpen = false;
    }

    private void ButtonWeekOffsetSettingsOpen_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.IsWeekOffsetSettingsOpen = true;
    }

    private void ButtonRestartAsAdmin_OnClick(object sender, RoutedEventArgs e)
    {
        if (!OperatingSystem.IsWindows() || PlatformServices.DesktopService.IsRunningAsAdmin)
            return;
        try
        {
            // AppBase.Restart 不带提权语义，重启为管理员身份必须自行拉起提权进程；
            // 新实例弹 UAC 确认成功后，当前实例正常退出。
            Process.Start(new ProcessStartInfo
            {
                FileName = AppBase.ExecutingEntrance,
                UseShellExecute = true,
                Verb = "runas"
            });
            AppBase.Current.Stop();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // 用户在 UAC 确认框点了取消，保持当前实例不动。
        }
    }

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        
    }

    private void GeneralSettingsPage_OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.SettingsService.Settings.PropertyChanged += SettingsOnPropertyChanged;
        ViewModel.PropertyChanged += ViewModelOnPropertyChanged;
    }
    
    private void GeneralSettingsPage_OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.SettingsService.Settings.PropertyChanged -= SettingsOnPropertyChanged;
        ViewModel.PropertyChanged -= ViewModelOnPropertyChanged;
    }

    private async void SettingsExpanderItemPreviewSplash_OnClick(object? sender, RoutedEventArgs e)
    {
        ViewModel.IsSplashPreviewing = true;
        try
        {
            IAppHost.GetService<ISplashService>().ResetSplashText();
            var splash = IAppHost.GetService<ISplashProvider>();
            await splash.StartSplash();
            await Task.Delay(5000);
            await splash.EndSplash();
        }
        finally
        {
            ViewModel.IsSplashPreviewing = false;
        }
    }
}


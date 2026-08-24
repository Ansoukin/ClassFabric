using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AsyncImageLoader;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using ClassFabric.Core;
using ClassFabric.Core.Abstractions.Controls;
using ClassFabric.Core.Abstractions.Services;
using ClassFabric.Core.Abstractions.Services.Management;
using ClassFabric.Core.Attributes;
using ClassFabric.Core.Controls;
using ClassFabric.Core.Enums.SettingsWindow;
using ClassFabric.Core.Helpers.UI;
using ClassFabric.Core.Models.UI;
using ClassFabric.Helpers;
using ClassFabric.Models;
using ClassFabric.Models.AllContributors;
using ClassFabric.Services;
using ClassFabric.Shared;
using ClassFabric.ViewModels.SettingsPages;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.Logging;

namespace ClassFabric.Views.SettingPages;

/// <summary>
/// AboutSettingsPage.xaml 的交互逻辑
/// </summary>
[HidePageTitle]
[SettingsPageInfo("about", "关于 ClassFabric", "\ue9e4", "\ue9e3", SettingsPageCategory.About)]
public partial class AboutSettingsPage : SettingsPageBase
{
    public static string DisplayVersion => $"{AppBase.AppVersion} (Codename {AppBase.AppCodeName})";
    
    public AboutSettingsViewModel ViewModel { get; } = IAppHost.GetService<AboutSettingsViewModel>();

    public AboutSettingsPage()
    {
        DataContext = this;
        InitializeComponent();
        var r = new StreamReader(AssetLoader.Open(new Uri("avares://ClassFabric/Assets/LICENSE.txt")));
        ViewModel.License = r.ReadToEnd();

        using var dependenciesStream = AssetLoader.Open(new Uri("avares://ClassFabric/Assets/dependencies.g.json"));
        ViewModel.ThirdPartyLibs = JsonSerializer.Deserialize<ObservableCollection<NuGetLicenseInfo>>(dependenciesStream) ?? [];
    }

    private void UriNavigationCommands_OnClick(object sender, RoutedEventArgs e)
    {
        var url = e.Source switch
        {
            SettingsExpanderItem s => s.CommandParameter?.ToString(),
            Button s => s.CommandParameter?.ToString(),
            _ => "classisland://app/test/"
        };
        IAppHost.TryGetService<IUriNavigationService>()?.NavigateWrapped(new Uri(url));
    }

    private void Hyperlink2_OnClick(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo()
        {
            FileName = "https://github.com/DuguSand/class_form",
            UseShellExecute = true
        });
    }

    private async void ButtonDiagnosticInfo_OnClick(object sender, RoutedEventArgs e)
    {
        var diagInfo = ViewModel.DiagnosticService.GetDiagnosticInfo();
        var dialog = new ContentDialog()
        {
            Title = "诊断信息",
            Content = new TextBox()
            {
                Text = diagInfo
            },
            IsSecondaryButtonEnabled = true,
            PrimaryButtonText = "确定",
            SecondaryButtonText = "复制",
            DefaultButton = ContentDialogButton.Primary
        };
        dialog.SecondaryButtonClick += ButtonCopyDiagnosticInfo_OnClick;
        await dialog.ShowAsync();
    }

    private async void ButtonCopyDiagnosticInfo_OnClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        bool success = false;
        try
        {
            await TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAsync(ViewModel.DiagnosticService.GetDiagnosticInfo());
            success = true;
        }
        catch (Exception ex)
        {
            App.GetService<ILogger<AboutSettingsPage>>().LogError(ex, "复制诊断信息失败。");
            ToastsHelper.ShowErrorToast(this, "复制失败，请全选诊断信息文本后手动复制。");
        }
        if (success)
        {
            ToastsHelper.ShowSuccessToast(this, "复制成功！");
        }
    }

    private async void ButtonContributors_OnClick(object sender, RoutedEventArgs e)
    {
        OpenDrawer("ContributorsDrawer");
        await RefreshContributors();
    }

    private void ButtonThirdPartyLibs_OnClick(object sender, RoutedEventArgs e)
    {
        OpenDrawer("ThirdPartyLibs");
    }

    private async Task RefreshContributors()
    {
        ViewModel.IsRefreshingContributors = true;
        try
        {
            ViewModel.SettingsService.Settings.ContributorsCache =
                await WebRequestHelper.Default.GetJson<AllContributorsRc>(new Uri(
                    "https://raw.githubusercontent.com/ClassIsland/ClassIsland/master/.all-contributorsrc"));
        }
        catch (Exception ex)
        {
            App.GetService<ILogger<AboutSettingsPage>>().LogError(ex, "无法获取贡献者名单。");
            this.ShowToast(new ToastMessage()
            {
                Severity = InfoBarSeverity.Error,
                Title = "无法获取贡献者名单",
                Message = ex.Message,
                AutoClose = false
            });
        }
        ViewModel.IsRefreshingContributors = false;
    }

    private async void ButtonRefreshContributors_OnClick(object sender, RoutedEventArgs e)
    {
        await RefreshContributors();
    }

    private void ButtonPrivacy_OnClick(object sender, RoutedEventArgs e)
    {
        new DocumentReaderWindow()
        {
            Source = new Uri("avares://ClassFabric/Assets/Documents/Privacy_.md"),
            Title = "ClassFabric 隐私政策"
        }.ShowDialog((TopLevel.GetTopLevel(this) as Window)!);
    }



    private async void SettingsExpanderItemShowOssLicense_OnClick(object? sender, RoutedEventArgs e)
    {
        var license = await new StreamReader(AssetLoader.Open(new Uri("avares://ClassFabric/Assets/LICENSE.txt")))
            .ReadToEndAsync();
        await new ContentDialog()
        {
            Title = "开放源代码许可",
            Content = new TextBlock()
            {
                Text = license
            },
            PrimaryButtonText = "关闭",
            DefaultButton = ContentDialogButton.Primary
        }.ShowAsync();
    }

    private async void InputElementReunion_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        ViewModel.AppInfoClickCount++;
        if (ViewModel.AppInfoClickCount < 20 || ViewModel.ManagementService.Policy.DisableEasterEggs) 
            return;
        TopLevel.GetTopLevel(this)?.Clipboard?
            .SetTextAsync("5oS/5oiR5Lus5Zyo6YKj6bKc6Iqx6Iqs6Iqz55qE6KW/6aOO5bC95aS06YeN6YCi44CC");
        var advancedImage = new AdvancedImage((Uri?)null)
        {
            Source = "https://res.classisland.tech/banners/reunion.webp",
        };
        var contentDialog = new ContentDialog()
        {
            Content = advancedImage,
            Title = "查看图片",
            PrimaryButtonText = "关闭",
            DefaultButton = ContentDialogButton.Primary,
            SecondaryButtonText = "在浏览器查看"
        };
        var r = await contentDialog.ShowAsync();
        if (r == ContentDialogResult.Secondary)
        {
            IAppHost.GetService<IUriNavigationService>().NavigateWrapped(new Uri("https://res.classisland.tech/banners/reunion.webp"));
        }
    }
}


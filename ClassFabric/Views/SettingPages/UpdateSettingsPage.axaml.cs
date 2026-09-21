using System;
using System.IO;
using System.Net.Mime;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Platform;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Extensions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Enums.SettingsWindow;
using ClassIsland.Enums.AppUpdating;
using ClassIsland.Services.AppUpdating;
using ClassIsland.Services.AppUpdating.Sources;
using ClassIsland.Shared;
using ClassIsland.Shared.Enums;
using ClassIsland.ViewModels.SettingsPages;
using DynamicData.Binding;
using FluentAvalonia.UI.Controls;
using ReactiveUI;

namespace ClassIsland.Views.SettingPages;

[SettingsPageInfo("update", "更新", "\ue161", "\ue160", SettingsPageCategory.Internal)]
public partial class UpdateSettingsPage : SettingsPageBase
{
    private IDisposable? _updateSettingsObserver;
    private IDisposable? _newVersionChangeLogObserver;
    private IDisposable? _distributionMetadataObserver;
    
    public UpdateSettingsPageViewModel ViewModel { get; } = IAppHost.GetService<UpdateSettingsPageViewModel>();

    public static readonly FuncValueConverter<UpdateStatus, string> UpdateStatusToIconGlyphConverter =
        new(x => x switch
        {
            UpdateStatus.UpToDate => "\ue1a1",
            UpdateStatus.UpdateAvailable => "\ue161",
            UpdateStatus.UpdateDownloaded => "\ue0d3",
            UpdateStatus.UpdateDeployed => "\ue163",
            _ => ""
        });
    
    public static readonly FuncValueConverter<UpdateStatus, string> UpdateStatusToMessageConverter =
        new(x => x switch
        {
            UpdateStatus.UpToDate => "您已更新到最新版本。",
            UpdateStatus.UpdateAvailable => "检测到更新。" ,
            UpdateStatus.UpdateDownloaded => "已准备好安装更新。",
            UpdateStatus.UpdateDeployed => "更新已就绪。",
            _ => ""
        });
    
    public static readonly FuncValueConverter<UpdateWorkingStatus, string> UpdateWorkingStatusToMessageConverter =
        new(x => x switch
        {
            UpdateWorkingStatus.Idle => "就绪",
            UpdateWorkingStatus.CheckingUpdates => "正在检查更新…",
            UpdateWorkingStatus.DownloadingUpdates => "正在下载更新…",
            UpdateWorkingStatus.ExtractingUpdates => "正在部署更新…",
            _ => "???"
        });

    public static readonly FuncValueConverter<DownloadState, string> DownloadStateToMessageConverter =
        new(x => x switch
        {
            DownloadState.Pending => "等待下载",
            DownloadState.Downloading => "正在下载",
            DownloadState.Completed => "完成",
            DownloadState.Error => "错误",
            _ => "???"
        });
    
    public UpdateSettingsPage()
    {
        DataContext = this;
        InitializeComponent();
    }
    
    private async void ButtonCheckUpdate_OnClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.UpdateService.CheckUpdateAsync();
    }

    private async void ButtonDownloadUpdate_OnClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.UpdateService.DownloadUpdateAsync();
        if (ViewModel.SettingsService.Settings.LastUpdateStatus == UpdateStatus.UpdateDownloaded)
        {
            await ViewModel.UpdateService.ExtractUpdateAsync();
        }
    }

    private void UpdateChannelInfo()
    {
        var settings = ViewModel.SettingsService.Settings;
        ViewModel.SelectedChannel =
            ViewModel.UpdateService.DistributionMetadata.Channels.TryGetValue(
                settings.SelectedUpdateChannelV3, out var v1)
                ? v1
                : ViewModel.SelectedChannel;
        var sourceId = UpdateSourceIds.Normalize(settings.UpdateSourceId);
        if ((sourceId == UpdateSourceIds.GitHub || sourceId == UpdateSourceIds.GitHubWithPhainonFallback) &&
            GitHubUpdateSource.IsGitHubChannelGuid(settings.SelectedUpdateChannelV3))
        {
            settings.GitHubUpdateChannel =
                GitHubUpdateSource.GetChannelIdFromGuid(settings.SelectedUpdateChannelV3);
        }
    }

    /// <summary>
    /// 更新通道列表是整体替换的字典。实测（Avalonia 12.1.1）在替换 <c>ItemsSource</c> 后，
    /// 下拉框会呈现新列表，但把选中项重置为空且不会按绑定源自动重新对齐，因此替换后必须显式对齐一次。
    /// 触发点：页面 <c>Loaded</c>、下拉框自身 <c>Loaded</c>、以及 <c>DistributionMetadata</c> 被替换时。
    /// </summary>
    private void AlignSelectedUpdateChannel()
    {
        UpdateChannelComboBox.SelectedValue = ViewModel.SettingsService.Settings.SelectedUpdateChannelV3;
    }

    /// <summary>
    /// 下拉框自身实例化完成后补一次对齐。新装（无缓存）时通道列表是「从空变满」的，页面 <c>Loaded</c> 里的对齐
    /// 有可能发生在该控件拿到 <c>DataContext</c> 之前而落空（Avalonia 不会在 ItemsSource 变化后重新解析
    /// <c>SelectedValue</c>），因此必须在控件自己的 <c>Loaded</c> 上再补一次。
    /// </summary>
    private void UpdateChannelComboBox_OnLoaded(object? sender, RoutedEventArgs e)
    {
        AlignSelectedUpdateChannel();
    }

    private void UpdateNewVersionChangeLog()
    {
        ViewModel.NewVersionChangeLogDocument =
            ViewModel.SettingsService.Settings.LastUpdateStatus != UpdateStatus.UpToDate
                ? ViewModel.UpdateService.DistributionInfo.ChangeLog
                : "";
    }

    private void Control_OnLoaded(object? sender, RoutedEventArgs e)
    {
        ViewModel.UpdateService.RefreshLocalChannelMetadata();
        UpdateChannelInfo();
        AlignSelectedUpdateChannel();
        UpdateNewVersionChangeLog();
        _updateSettingsObserver ??= ViewModel.SettingsService.Settings
            .ObservableForProperty(x => x.SelectedUpdateChannelV3)
            .Subscribe(_ => UpdateChannelInfo());
        _distributionMetadataObserver ??= ViewModel.UpdateService
            .ObservableForProperty(x => x.DistributionMetadata)
            .Subscribe(_ => AlignSelectedUpdateChannel());
        _newVersionChangeLogObserver ??= ViewModel.UpdateService
            .WhenAnyPropertyChanged()
            .Subscribe(_ => UpdateNewVersionChangeLog());
        
    }


    private void Control_OnUnloaded(object? sender, RoutedEventArgs e)
    {
        _updateSettingsObserver?.Dispose();
        _updateSettingsObserver = null;
        _newVersionChangeLogObserver?.Dispose();
        _newVersionChangeLogObserver = null;
        _distributionMetadataObserver?.Dispose();
        _distributionMetadataObserver = null;
    }

    private void ButtonOpenDownloadTasks_OnClick(object? sender, RoutedEventArgs e)
    {
        OpenDrawer("DownloadInfoDrawer");
    }

    private async void ButtonCancelDownload_OnClick(object? sender, RoutedEventArgs e)
    {
        await ViewModel.UpdateService.StopDownloading();
    }

    private async void ButtonDeployUpdate_OnClick(object? sender, RoutedEventArgs e)
    {
        await ViewModel.UpdateService.ExtractUpdateAsync();
    }

    private void InfoBarError_OnCloseButtonClick(FAInfoBar sender, EventArgs args)
    {
        ViewModel.UpdateService.NetworkErrorException = null;
    }
    
    private void InfoBarDeployError_OnCloseButtonClick(FAInfoBar sender, EventArgs args)
    {
        ViewModel.UpdateService.DeployErrorException = null;
    }

    private void ButtonRestart_OnClick(object? sender, RoutedEventArgs e)
    {
        AppBase.Current.Restart(["-m"], true);
    }

    private async void SettingsExpanderItemCheckUpdateForce_OnClick(object? sender, RoutedEventArgs e)
    {
        await ViewModel.UpdateService.CheckUpdateAsync(true);
    }

    private async void ButtonShowChangeLogs_OnClick(object? sender, RoutedEventArgs e)
    {
ViewModel.ChangeLogDocument = await AssetLoader.ReadAllTextAsync(
            new Uri("avares://ClassFabric/Assets/Documents/ChangeLog.md"));
        OpenDrawer("ChangeLogDrawer");
    }

    private async void MenuItemDownloadOnly_OnClick(object? sender, RoutedEventArgs e)
    {
        await ViewModel.UpdateService.DownloadUpdateAsync();
    }
}

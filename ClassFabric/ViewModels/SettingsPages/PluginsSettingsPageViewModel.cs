using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using ClassIsland.Controls;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.ComponentModels;
using ClassIsland.Core.Models.Plugin;
using ClassIsland.Services;
using ClassIsland.Views.SettingPages;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace ClassIsland.ViewModels.SettingsPages;

public partial class PluginsSettingsPageViewModel : ObservableRecipient
{
    public IPluginService PluginService { get; }
    public IPluginMarketService PluginMarketService { get; }
    public SettingsService SettingsService { get; }
    public ILogger<PluginsSettingsPage> Logger { get; }
    
    [ObservableProperty] private PluginInfo? _selectedPluginInfo;
    [ObservableProperty] private Avalonia.Controls.Control? _readmeContent;
    [ObservableProperty] private ObservableCollection<string> _galleryImages = new();
    [ObservableProperty] private int _gallerySelectedIndex;
    [ObservableProperty] private string _overviewTitle = "";
    [ObservableProperty] private Avalonia.Controls.Control? _badgeRow;
    [ObservableProperty] private bool _isPluginOperationsPopupOpened = false;
    [ObservableProperty] private bool _isPluginMarketOperationsPopupOpened = false;
    [ObservableProperty] private PluginIndexInfo? _selectedPluginIndexInfo;
    [ObservableProperty] private int _pluginCategoryIndex = 1;
    [ObservableProperty] private string _pluginFilterText = "";
    [ObservableProperty] private bool _isLoadingDocument = false;
    [ObservableProperty] private bool _isInstallingLocalPlugin = false;
    [ObservableProperty] private bool _isDetailsShown = false;
    [ObservableProperty] private bool _isDragEntering = false;
    [ObservableProperty] private bool _isDragInstallValid = false;
    [ObservableProperty] private int _dragInstallTotalCount = 0;
    [ObservableProperty] private int _dragInstallSupportedCount = 0;
    [ObservableProperty] private string _dragInstallHintText = "将插件拖入到此处，松手即可安装。";
    [ObservableProperty] private string _dragInstallSubHintText = "";
    [ObservableProperty] private bool _pluginListBoxHasItems = false;

    /// <summary>README 没有图片时轮播整体隐藏；有图时供绑定判断。</summary>
    public bool HasGalleryImages => GalleryImages.Count > 0;

    /// <summary>轮播当前显示的图片地址，下标越界一律给空串（切插件瞬间下标还没复位）。</summary>
    public string CurrentGalleryImage =>
        GallerySelectedIndex >= 0 && GallerySelectedIndex < GalleryImages.Count
            ? GalleryImages[GallerySelectedIndex]
            : "";

    partial void OnGalleryImagesChanged(ObservableCollection<string> value)
    {
        OnPropertyChanged(nameof(HasGalleryImages));
        OnPropertyChanged(nameof(CurrentGalleryImage));
    }

    partial void OnGallerySelectedIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentGalleryImage));
    }

    /// <summary>
    /// 应用 README 清洗结果并渲染控件树。
    /// 不走 MarkdownScrollViewer：它的模板自带滚动容器，会和详情页的统一滚动域抢滚轮、
    /// 在自适应高度下塌陷叠字；引擎直出控件树塞进 ContentControl，滚动完全归外层。
    /// </summary>
    internal void ApplyReadme(ClassIsland.Services.MarketReadmeResult result)
    {
        OverviewTitle = result.OverviewTitle;
        GalleryImages = new ObservableCollection<string>(result.GalleryImages);
        GallerySelectedIndex = 0;
        BadgeRow = BuildBadgeRow(result.Badges);
        try
        {
            ReadmeContent = ClassIsland.Core.Helpers.MarkdownConvertHelper.Engine.Transform(result.BodyMarkdown);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "README 渲染失败");
            ReadmeContent = new Avalonia.Controls.TextBlock
            {
                Text = string.Format(ClassFabric.Assets.Localization.SettingsPage.Plugins.Localization.ReadmeLoadFailed, e.Message),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Opacity = 0.7
            };
        }
    }

    public void ClearReadmeExtras()
    {
        OverviewTitle = "";
        GalleryImages = new ObservableCollection<string>();
        GallerySelectedIndex = 0;
        BadgeRow = null;
        ReadmeContent = null;
    }

    /// <summary>
    /// 徽章行：README 抽出的 shields 徽标横排展示，有外链的可点击跳转。
    /// 照 ReadmeContent 的方式在代码里构建控件树，不为两个展示字段新增公开类型——
    /// app 程序集新增公开类型要连带补兼容垫片转发声明，不值当。
    /// </summary>
    private static Avalonia.Controls.Control? BuildBadgeRow(IReadOnlyList<MarketBadge> badges)
    {
        if (badges.Count == 0)
        {
            return null;
        }
        var panel = new Avalonia.Controls.WrapPanel { Margin = new Avalonia.Thickness(0, 2, 0, 8) };
        foreach (var badge in badges)
        {
            // AdvancedImage 只会走 Skia 位图解码，README 徽章/图表大量是 SVG，
            // 换 SvgAwareImage（SVG 内容探测分叉解码，见 Controls/SvgAwareImage.cs）
            var image = new SvgAwareImage
            {
                Source = badge.ImageUrl,
                MaxHeight = 22,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            if (badge.LinkUrl is null)
            {
                image.Margin = new Avalonia.Thickness(0, 0, 6, 2);
                panel.Children.Add(image);
                continue;
            }
            var button = new Avalonia.Controls.Button
            {
                Content = image,
                Padding = new Avalonia.Thickness(0),
                BorderThickness = new Avalonia.Thickness(0),
                Background = Avalonia.Media.Brushes.Transparent,
                Margin = new Avalonia.Thickness(0, 0, 6, 2),
                Command = ClassIsland.Core.Commands.UriNavigationCommands.UriNavigationCommand,
                CommandParameter = badge.LinkUrl,
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
            };
            panel.Children.Add(button);
        }
        return panel;
    }

    private ReadOnlyObservableCollection<KeyValuePair<string, PluginInfo>> _mergedPluginsFiltered = null!;
    public ReadOnlyObservableCollection<KeyValuePair<string, PluginInfo>> MergedPluginsFiltered => _mergedPluginsFiltered;

    private SyncDictionaryList<string, string> _officialPluginMirrors = null!;
    public SyncDictionaryList<string, string> OfficialPluginMirrors
    {
        get => _officialPluginMirrors;
        private set
        {
            if (ReferenceEquals(_officialPluginMirrors, value))
            {
                return;
            }

            _officialPluginMirrors?.Dispose();
            _officialPluginMirrors = value;
            OnPropertyChanged();
        }
    }

    private SyncDictionaryList<string, PluginInfo> _mergedPlugins = null!;
    public SyncDictionaryList<string, PluginInfo> MergedPlugins => _mergedPlugins;

    private IDisposable? _officialIndexMirrorsSubscription;
    private IDisposable? _mergedPluginsSubscription;
    private bool _isActivated;

    /// <inheritdoc/>
    public PluginsSettingsPageViewModel(IPluginService pluginService, IPluginMarketService pluginMarketService, SettingsService settingsService, ILogger<PluginsSettingsPage> logger)
    {
        PluginService = pluginService;
        PluginMarketService = pluginMarketService;
        SettingsService = settingsService;
        Logger = logger;

        Activate();
        UpdateOfficialPluginSources();
    }

    /// <summary>
    /// 订阅全局服务和集合，用于更新插件列表。页面加载时调用。
    /// </summary>
    public void Activate()
    {
        if (_isActivated)
        {
            return;
        }

        _isActivated = true;
        _mergedPlugins = new SyncDictionaryList<string, PluginInfo>(PluginMarketService.MergedPlugins, () => "");
        OnPropertyChanged(nameof(MergedPlugins));
        UpdateMergedPlugins();

        _officialIndexMirrorsSubscription = SettingsService.Settings
            .ObservableForProperty(x => x.OfficialIndexMirrors)
            .Subscribe(_ => UpdateOfficialPluginSources());
    }

    /// <summary>
    /// 释放对全局服务和集合的订阅，避免 ViewModel 被静态服务长期保留。页面卸载时调用。
    /// </summary>
    public void Deactivate()
    {
        if (!_isActivated)
        {
            return;
        }

        _isActivated = false;

        _officialIndexMirrorsSubscription?.Dispose();
        _officialIndexMirrorsSubscription = null;

        _mergedPluginsSubscription?.Dispose();
        _mergedPluginsSubscription = null;
        _mergedPluginsFiltered = null!;
        OnPropertyChanged(nameof(MergedPluginsFiltered));

        _mergedPlugins?.Dispose();
        _mergedPlugins = null!;
        OnPropertyChanged(nameof(MergedPlugins));

        OfficialPluginMirrors = null!;
    }

    public void UpdateMergedPlugins()
    {
        if (_mergedPluginsFiltered != null)
            return;

        var pluginFilter = this
            .WhenAnyValue(x => x.PluginFilterText, x => x.PluginCategoryIndex)
            .Select(_ => new Func<KeyValuePair<string, PluginInfo>, bool>(PluginSourceFilter));

        _mergedPluginsSubscription = MergedPlugins.List
            .ToObservableChangeSet()
            .Filter(pluginFilter)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out _mergedPluginsFiltered)
            .Subscribe();

        OnPropertyChanged(nameof(MergedPluginsFiltered));
    }

    private void UpdateOfficialPluginSources()
    {
        OfficialPluginMirrors =
            new SyncDictionaryList<string, string>(SettingsService.Settings.OfficialIndexMirrors, () => "");
    }
    
    private bool PluginSourceFilter(KeyValuePair<string, PluginInfo> kvp)
    {
        var info = kvp.Value;
        if (!info.IsLocal && PluginCategoryIndex == 1)
        {
            return false;
        }
        if (!info.IsAvailableOnMarket && PluginCategoryIndex == 0)
        {
            return false;
        }
        
        var filter = PluginFilterText;
        if (string.IsNullOrWhiteSpace(filter))
            return true;
        return info.Manifest.Id.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
               info.Manifest.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
               info.Manifest.Description.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }
}

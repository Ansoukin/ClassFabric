using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.ComponentModels;
using ClassIsland.Core.Models.Plugin;
using CommunityToolkit.Mvvm.ComponentModel;
using ClassIsland.Core.Models.XamlTheme;
using ClassIsland.Services;
using DynamicData;
using DynamicData.Binding;

namespace ClassIsland.ViewModels.SettingsPages;

public partial class ThemesSettingsViewModel : ObservableObject
{
    public IXamlThemeService XamlThemeService { get; }
    public IPluginMarketService PluginMarketService { get; }
    public SettingsService SettingsService { get; }

    [ObservableProperty] private ThemeInfo? _selectedThemeInfo;
    [ObservableProperty] private bool _isThemeOperationsPopupOpened = false;
    [ObservableProperty] private bool _isThemeMarketOperationsPopupOpened = false;
    [ObservableProperty] private ThemeIndexItem? _selectedThemeIndexInfo;
    [ObservableProperty] private int _themeCategoryIndex = 1;
    [ObservableProperty] private string _themeFilterText = "";
    [ObservableProperty] private bool _isDragEntering = false;

    [ObservableProperty] private IObservableList<KeyValuePair<string, ThemeInfo>> _mergedThemesFiltered = null!;

    /// <summary>
    /// 已启用且带配置视图的主题。「主题配置」分类只对这些主题展示配置界面；
    /// 集合为空时该分类标签整体隐藏。
    /// </summary>
    [ObservableProperty] private ObservableCollection<KeyValuePair<string, ThemeInfo>> _configurableEnabledThemes = [];

    /// <summary>当前是否停在「主题配置」分类，用于切换主题列表与配置视图的显示。</summary>
    public bool IsThemeConfigCategory => ThemeCategoryIndex == 2;

    /// <summary>是否存在可配置的已启用主题，决定「主题配置」分类标签是否显示。</summary>
    public bool HasConfigurableEnabledThemes => ConfigurableEnabledThemes.Count > 0;

    /// <summary>分类下标变化时刷新派生显示属性。</summary>
    partial void OnThemeCategoryIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsThemeConfigCategory));
        OnPropertyChanged(nameof(HasConfigurableEnabledThemes));
    }

    public SyncDictionaryList<string, ThemeInfo> MergedThemes { get; set; } = null!;

    /// <inheritdoc/>
    public ThemesSettingsViewModel(IXamlThemeService xamlThemeService, IPluginMarketService pluginMarketService, SettingsService settingsService)
    {
        XamlThemeService = xamlThemeService;
        PluginMarketService = pluginMarketService;
        SettingsService = settingsService;

        UpdateMergedThemes();
    }

    public void UpdateMergedThemes()
    {
        MergedThemes = new SyncDictionaryList<string, ThemeInfo>(XamlThemeService.MergedThemes, () => "");
        MergedThemesFiltered = MergedThemes.List
            .ToObservableChangeSet()
            .Filter(ThemeSourceFilter)
            .AsObservableList();
        UpdateConfigurableThemes();
    }

    /// <summary>
    /// 重算可配置主题集合：启用中的主题 ∩ 注册了配置视图工厂的主题。
    /// </summary>
    public void UpdateConfigurableThemes()
    {
        var items = XamlThemeService.EnabledThemes
            .Select(id => XamlThemeService.Themes.FirstOrDefault(t => t.Manifest.Id == id))
            .OfType<ThemeInfo>()
            .Where(t => t.SettingsViewFactory != null)
            .Select(t => new KeyValuePair<string, ThemeInfo>(t.Manifest.Id, t))
            .ToList();
        ConfigurableEnabledThemes.Clear();
        foreach (var item in items)
        {
            ConfigurableEnabledThemes.Add(item);
        }
        OnPropertyChanged(nameof(HasConfigurableEnabledThemes));
    }

    private bool ThemeSourceFilter(KeyValuePair<string, ThemeInfo> kvp)
    {
        var info = kvp.Value;
        if (!info.IsLocal && ThemeCategoryIndex == 1)
        {
            return false;
        }
        if (!info.IsAvailableOnMarket && ThemeCategoryIndex == 0)
        {
            return false;
        }

        var filter = ThemeFilterText;
        if (string.IsNullOrWhiteSpace(filter))
            return true;
        return info.Manifest.Id.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                 info.Manifest.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                 info.Manifest.Description.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

}

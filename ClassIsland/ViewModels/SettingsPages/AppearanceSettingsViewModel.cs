using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using ClassIsland.Converters;
using ClassIsland.Core;
using ClassIsland.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.ViewModels.SettingsPages;

public partial class AppearanceSettingsViewModel(SettingsService settingsService) : ObservableRecipient
{
    [ObservableProperty] private string _fontSizeTestText = "风带来故事的种子，时间使之发芽。The quick brown fox jumps over a lazy dog.";
    [ObservableProperty] private bool _isSelectingBackgroundImage;
    [ObservableProperty] private string _backgroundImageError = "";

    // DirectWrite 缓存可能残留已卸载字体的幽灵族，逐项渲染会炸布局，构造列表时先过滤掉；
    // 键控族（fonts: 系）与名字带 URI 特征（://）的怪族一并禁入——真实字体族名不会包含协议分隔符。
    public ObservableCollection<FontFamily> FontFamilies { get; } =
        new([..FontManager.Current.SystemFonts
            .Where(f => f.Key == null && !f.Name.Contains("://") && SafeFontFamilyConverter.IsRenderable(f)),
            MainWindow.DefaultFontFamily]);

    public SettingsService SettingsService { get; } = settingsService;
}

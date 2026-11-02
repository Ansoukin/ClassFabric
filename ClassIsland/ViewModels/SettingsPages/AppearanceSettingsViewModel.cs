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

    // DirectWrite 的字体缓存可能残留已卸载字体的枚举项：能枚举到、字形却创建不了，
    // 而 Avalonia 对带键字体族查找失败时不走兜底，逐项渲染会直接抛异常。构造列表时过滤：
    // 带键族（fonts: 系）与名字含 "://" 的项一并禁入，真实字体族名不会包含协议分隔符。
    public ObservableCollection<FontFamily> FontFamilies { get; } =
        new([..FontManager.Current.SystemFonts
            .Where(f => f.Key == null && !f.Name.Contains("://") && SafeFontFamilyConverter.IsRenderable(f)),
            MainWindow.DefaultFontFamily]);

    public SettingsService SettingsService { get; } = settingsService;
}

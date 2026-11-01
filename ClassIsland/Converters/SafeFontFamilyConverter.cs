using System;
using System.Collections.Concurrent;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ClassIsland.Converters;

/// <summary>
/// 字体渲染安全闸：DirectWrite 的字体缓存里可能残留已卸载字体的幽灵族（能被枚举、加载不了），
/// 而 Avalonia 对带键字体族（如 fonts:SystemFonts 里的项）查找失败时不走兜底，
/// Typeface.GlyphTypeface 会直接抛 InvalidOperationException 炸掉整个布局（字体下拉逐项渲染即崩）。
/// 渲染前先验证字形可创建，失败一律回落应用默认字体。验证结果按族名缓存，避免逐项加载的重复开销。
/// </summary>
public class SafeFontFamilyConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<string, bool> RenderableCache = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is FontFamily family && IsRenderable(family) ? family : MainWindow.DefaultFontFamily;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is FontFamily family && IsRenderable(family) ? family : MainWindow.DefaultFontFamily;
    }

    internal static bool IsRenderable(FontFamily family)
    {
        return RenderableCache.GetOrAdd(family.ToString(), key =>
        {
            try
            {
                return FontManager.Current.TryGetGlyphTypeface(new Typeface(key), out var glyphTypeface);
            }
            catch (Exception)
            {
                return false;
            }
        });
    }
}

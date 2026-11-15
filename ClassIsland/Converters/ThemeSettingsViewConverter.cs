using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using ClassIsland.Core.Models.XamlTheme;

namespace ClassIsland.Converters;

/// <summary>
/// 调主题的配置视图工厂（ThemeInfo.SettingsViewFactory）生成配置界面实例。
/// 「主题配置」分类用：XAML 无法直接调用委托，经此转换器把 ThemeInfo 换成配置视图。
/// 工厂只在列表重排时调用，配置数据绑定到全局单例，视图重建不丢设置。
/// </summary>
public class ThemeSettingsViewConverter : IValueConverter
{
    public static readonly ThemeSettingsViewConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (value as ThemeInfo)?.SettingsViewFactory?.Invoke();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

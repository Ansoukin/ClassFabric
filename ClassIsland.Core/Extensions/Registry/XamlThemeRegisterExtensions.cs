using Avalonia.Controls;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Models.XamlTheme;
using Microsoft.Extensions.DependencyInjection;

namespace ClassIsland.Core.Extensions.Registry;

/// <summary>
/// 用于注册 Xaml 主题的 <see cref="IServiceCollection"/>扩展。
/// </summary>
public static class XamlThemeRegisterExtensions
{
    /// <summary>
    /// 注册 Xaml 主题。注册后，此主题将在主题界面中显示。
    /// </summary>
    /// <param name="services"><see cref="IServiceCollection"/>实例</param>
    /// <param name="uri">主题资源的 Uri</param>
    /// <param name="manifest">主题清单</param>
    /// <param name="settingsViewFactory">主题配置视图工厂；主题带有可配置项时提供，为 null 表示无配置项。</param>
    /// <returns>原来的<see cref="IServiceCollection"/>实例</returns>
    public static IServiceCollection AddXamlTheme(this IServiceCollection services, Uri uri, ThemeManifest manifest, Func<Control>? settingsViewFactory = null)
    {
        IXamlThemeService.IntegratedThemes.Add(new ThemeInfo()
        {
            ThemeUri = uri,
            Manifest = manifest,
            IsExternal = false,
            RealBannerPath = manifest.Banner,
            IsLocal = true,
            SettingsViewFactory = settingsViewFactory
        });
        return services;
    }
}

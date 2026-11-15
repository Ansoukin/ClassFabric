using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Core.Models.Plugin;
using ClassIsland.Services;
using ClassIsland.Shared;
using ClassIsland.UltraCode;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClassIsland.Desktop;

/// <summary>
/// 内置插件装配。插件工程只被 Desktop 引用，主工程不感知插件类型，
/// 经 App.ConfigureBuiltInPlugins 钩子（在 PluginService.InitializePlugins 之后）注册。
/// </summary>
internal static class BuiltInPlugins
{
    public static void Register(HostBuilderContext context, IServiceCollection services)
    {
        RegisterPixelFlow(context, services);
#if Platforms_Windows
        RegisterFancyAnima(context, services);
#endif
    }

    private static void RegisterPixelFlow(HostBuilderContext context, IServiceCollection services)
    {
        services.AddBuiltInPlugin<PixelFlowPlugin>(context, new PluginInfo
        {
            Manifest = new PluginManifest
            {
                Id = "gordon.ultracode",
                Name = "像素流动",
                Description = "仿 Claude Code UltraCode 的像素流动动画，替换提醒的遮罩层与正文层背景。配色跟随应用主题色，倒计时以彩带长度表示剩余时间。",
                Version = "1.1.0.0",
                ApiVersion = "2.0.0.0",
                Author = "GordonYoung",
                Url = "https://github.com/Gordonynh/PixelFlow"
            }
        });
    }

#if Platforms_Windows
    private static void RegisterFancyAnima(HostBuilderContext context, IServiceCollection services)
    {
        services.AddBuiltInPlugin<FancyAnima.Plugin>(context, new PluginInfo
        {
            Manifest = new PluginManifest
            {
                Id = "FancyAnima",
                Name = "FancyAnima",
                Description = "基于 CPU 渲染绘制的高性能提醒特效，低端设备更流畅。",
                Version = "1.0.2.0",
                ApiVersion = "2.0.0.0",
                Author = "Tiny-Nick",
                Url = "https://github.com/Tiny-Nick/FancyAnima"
            }
        }, config =>
        {
            // 委托懒求值：播放时才取设置值，插件只见 Func，不引用主工程。
            config.FallbackThemeColor = () => IAppHost.GetService<SettingsService>().Settings.PrimaryColor;
        });
    }
#endif
}

using System;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Core.Models.XamlTheme;
using ClassIsland.UltraCodeShared;
using ClassIsland.UltraCode.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClassIsland.UltraCode;

/// <summary>
/// 像素流动主题（原社区插件 PixelFlow @GordonYoung，MIT）的内置插件入口。
/// </summary>
/// <remarks>
/// 往主题列表里挂一个内嵌的 XAML 主题，并注册主题配置视图。
/// 主题里的模板引用了本程序集的 <c>UltraCodeField</c>，
/// 所以控件必须随插件一起编译，不能只发一份 axaml。
/// 内置版经 AddBuiltInPlugin 注册，程序集常驻默认 AssemblyLoadContext，
/// avares 资源直接可解析，无需社区版那套 ALC 回调。
/// </remarks>
public class PixelFlowPlugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        // 动画参数存在内置插件配置目录（Config/Plugins/BuildIn/gordon.ultracode/），
        // 与社区版配置互不覆盖。
        UltraCodeOptions.Initialize(PluginConfigFolder);
        UltraCodeOptions.PaletteInvalidated += (_, _) => UltraCodePalette.Invalidate();

        // 本插件是这套参数的所有者，把可写的设置单例交给调参入口，
        // 调色板取色时从这里读滑块值。
        UltraCodeTuning.Current = UltraCodeOptions.Current;

        services.AddXamlTheme(
            new Uri("avares://ClassIsland.Plugins.PixelFlow/Themes/Styles.axaml"),
            new ThemeManifest
            {
                Id = "gordon.ultracode",
                Name = "像素流动",
                Description = "仿 Claude Code UltraCode 的像素流动动画，替换提醒的遮罩层与正文层背景。"
                              + "配色跟随应用主题色，倒计时以彩带长度表示剩余时间。",
                Author = "GordonYoung",
                Version = "1.1.0.0",
                Url = "https://github.com/Gordonynh/PixelFlow",
                // 预览图暂用 Fluent 的占位，等专用预览图做好后替换。
                Banner = "avares://ClassIsland/Assets/XamlThemePreviews/ClassIsland.fluent.png"
            },
            () => new PixelFlowSettingsView());
    }
}

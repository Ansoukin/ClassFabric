using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Shared.Helpers;
using FancyAnima.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FancyAnima;

/// <summary>
/// 涟漪特效（原社区插件 FancyAnima @Tiny-Nick，MIT）的内置插件入口。
/// 提醒播放时在主窗口所在显示器播放一圈涟漪扩散动画。
/// </summary>
/// <remarks>
/// 配置入口在「主界面-窗口」设置页（经 IFancyAnimaSettings 契约），不注册原插件的独立设置页。
/// 触发完全归提醒管线：特效风格选为涟漪时，NotificationWorkerService 经
/// INotificationRippleEffect 入口播放；主题主色兜底来自宿主注入的 BuiltInConfig 委托，
/// 插件不引用主工程。
/// </remarks>
public class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        var settings = ConfigureFileHelper.LoadConfig<Settings.Settings>(Path.Combine(PluginConfigFolder, "Settings.json"));
        settings.PropertyChanged += (_, _) =>
        {
            ConfigureFileHelper.SaveConfig(Path.Combine(PluginConfigFolder, "Settings.json"), settings);
        };
        services.AddSingleton(settings);
        services.AddSingleton<IFancyAnimaSettings>(settings);

        // 主题主色兜底委托：未注入时用固定蓝。
        var fallbackThemeColor = BuiltInConfig?.FallbackThemeColor
                                 ?? (() => Avalonia.Media.Color.FromRgb(120, 180, 255));

        services.AddSingleton<FancyAnimaHost>(sp => new FancyAnimaHost(
            sp.GetRequiredService<Settings.Settings>(),
            sp.GetRequiredService<ILogger<FancyAnimaHost>>(),
            fallbackThemeColor));
        services.AddHostedService(sp => sp.GetRequiredService<FancyAnimaHost>());
        // 提醒管线经此入口按特效风格触发涟漪
        services.AddSingleton<INotificationRippleEffect>(sp => sp.GetRequiredService<FancyAnimaHost>());
    }
}

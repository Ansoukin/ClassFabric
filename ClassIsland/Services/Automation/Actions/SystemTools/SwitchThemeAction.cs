using System;
using System.Linq;
using System.Threading.Tasks;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Icons;
using ClassIsland.Core.Models.Notification;
using ClassIsland.Models.Actions.SystemTools;
using ClassIsland.Platforms.Abstraction;
using ClassIsland.Services.NotificationProviders;
using ClassIsland.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services.Automation.Actions.SystemTools;

[AutomationCategory("系统个性化")]
[ActionInfo("SystemTools.SwitchTheme", "切换主题色", FluentIcons.WandRegular, false)]
public class SwitchThemeAction(ILogger<SwitchThemeAction> logger) : ActionBase<ThemeSettings>
{
    private readonly ILogger<SwitchThemeAction> _logger = logger;

    static SystemToolsNotificationProvider? NotificationProvider =>
        IAppHost.Host?.Services.GetServices<IHostedService>()
            .OfType<SystemToolsNotificationProvider>().FirstOrDefault();

    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        if (Settings == null) return;

        try
        {
            // 注册表 Personalize 键的写入下沉到平台服务；浅色/深色的取值沿用插件设置里的中文文案。
            PlatformServices.SystemEnvironmentService.SetSystemTheme(Settings.Theme == "浅色");
            _logger.LogInformation("切换主题成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换主题失败");
            throw;
        }

        if (Settings.NotifyOnExecute)
        {
            NotificationProvider?.ShowNotification(new NotificationRequest
            {
                MaskContent = NotificationContent.CreateTwoIconsMask("已自动切换系统主题色", FluentIcons.IotAlertRegular, "")
            });
        }
    }
}

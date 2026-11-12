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

/// <summary>
/// 显示桌面。模拟 Win+D 组合键，键序与原插件一致：LWIN 先按下、最后释放。
/// SystemInputService 在非 Windows 平台是空操作 stub，无需再判平台。
/// </summary>
[AutomationCategory("显示设置")]
[ActionInfo("SystemTools.ShowDesktop", "显示桌面", FluentIcons.DesktopRegular, false)]
public class ShowDesktopAction(ILogger<ShowDesktopAction> logger) : ActionBase<ShortcutKeyNotificationSettings>
{
    private const ushort VK_LWIN = 0x5B;
    private const ushort VK_D = 0x44;

    private readonly ILogger<ShowDesktopAction> _logger = logger;

    protected override async Task OnInvoke()
    {
        try
        {
            _logger.LogInformation("正在模拟按下 Win+D 显示桌面");

            var input = PlatformServices.SystemInputService;
            input.KeyDown(VK_LWIN);
            await Task.Delay(20);

            input.KeyDown(VK_D);
            await Task.Delay(20);

            input.KeyUp(VK_D);
            await Task.Delay(20);

            input.KeyUp(VK_LWIN);
            _logger.LogInformation("Win+D 已成功发送");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送 Win+D 失败");
            throw;
        }

        if (Settings.NotifyOnExecute)
            IAppHost.Host?.Services.GetServices<IHostedService>().OfType<SystemToolsNotificationProvider>()
                .FirstOrDefault()?.ShowNotification(new NotificationRequest
                {
                    MaskContent = NotificationContent.CreateTwoIconsMask("已执行显示桌面操作", FluentIcons.IotAlertRegular, "")
                });

        await base.OnInvoke();
    }
}

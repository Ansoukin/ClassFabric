using System;
using System.Linq;
using System.Threading.Tasks;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Icons;
using ClassIsland.Core.Models.Notification;
using ClassIsland.Models.Actions.SystemTools;
using ClassIsland.Services.NotificationProviders;
using ClassIsland.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System.Diagnostics;
namespace ClassIsland.Services.Automation.Actions.SystemTools;

/// <summary>
/// 锁定屏幕。rundll32 调 user32.LockWorkStation，仅 Windows 有效。
/// </summary>
[AutomationCategory("电源选项")]
[ActionInfo("SystemTools.LockScreen", "锁定屏幕", FluentIcons.LockClosedRegular, false)]
public class LockScreenAction(ILogger<LockScreenAction> logger) : ActionBase<ShortcutKeyNotificationSettings>
{
    private readonly ILogger<LockScreenAction> _logger = logger;

    protected override async Task OnInvoke()
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogWarning("LockWorkStation 仅支持 Windows，跳过锁定屏幕。");
            await base.OnInvoke();
            return;
        }

        try
        {
            _logger.LogInformation("正在执行锁定屏幕命令");

            var psi = new ProcessStartInfo
            {
                FileName = "rundll32.exe",
                Arguments = "user32.dll,LockWorkStation",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(psi);

            _logger.LogInformation("屏幕已锁定");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "锁定屏幕失败");
            throw;
        }

        if (Settings.NotifyOnExecute)
            IAppHost.Host?.Services.GetServices<IHostedService>().OfType<SystemToolsNotificationProvider>()
                .FirstOrDefault()?.ShowNotification(new NotificationRequest
                {
                    MaskContent = NotificationContent.CreateTwoIconsMask("已锁定屏幕", FluentIcons.IotAlertRegular, "")
                });

        await base.OnInvoke();
    }
}

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
/// 取消关机计划。shutdown /a 只能撤掉「已排定延迟」的关机命令，对立即执行 (t 0) 的无效。
/// </summary>
[AutomationCategory("电源选项")]
[ActionInfo("SystemTools.CancelShutdown", "取消关机计划", FluentIcons.ClockDismissRegular, false)]
public class CancelShutdownAction(ILogger<CancelShutdownAction> logger) : ActionBase<ShortcutKeyNotificationSettings>
{
    private readonly ILogger<CancelShutdownAction> _logger = logger;

    protected override async Task OnInvoke()
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogWarning("shutdown 命令仅支持 Windows，跳过执行。");
            await base.OnInvoke();
            return;
        }

        try
        {
            _logger.LogInformation("正在执行取消关机命令");

            var psi = new ProcessStartInfo
            {
                FileName = "shutdown",
                Arguments = "-a",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(psi);

            _logger.LogInformation("关机已取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取消关机失败");
            throw;
        }

        if (Settings.NotifyOnExecute)
            IAppHost.Host?.Services.GetServices<IHostedService>().OfType<SystemToolsNotificationProvider>()
                .FirstOrDefault()?.ShowNotification(new NotificationRequest
                {
                    MaskContent = NotificationContent.CreateTwoIconsMask("已取消关机计划", FluentIcons.IotAlertRegular, "")
                });

        await base.OnInvoke();
    }
}

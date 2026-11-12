using System;
using System.Diagnostics;
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
/// 计时关机。调系统 shutdown 命令排计划；ShowPrompt=false 时再模拟一次回车关掉系统提示框，
/// 插件版用 WinForms 的 SendKeys，主工程不开 WinForms，换成 SystemInputService 的回车键模拟。
/// </summary>
[AutomationCategory("电源选项")]
[ActionInfo("SystemTools.Shutdown", "计时关机", FluentIcons.ClockRegular, false)]
public class ShutdownAction(ILogger<ShutdownAction> logger) : ActionBase<ShutdownSettings>
{
    private const ushort VK_RETURN = 0x0D;

    private readonly ILogger<ShutdownAction> _logger = logger;

    protected override async Task OnInvoke()
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogWarning("shutdown 命令仅支持 Windows，跳过执行。");
            await base.OnInvoke();
            return;
        }

        if (Settings == null) return;

        if (Settings.Seconds < 0) return;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "shutdown",
                Arguments = $"-s -t {Settings.Seconds}",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(psi);

            if (!Settings.ShowPrompt)
            {
                await Task.Delay(300);
                var input = PlatformServices.SystemInputService;
                input.KeyDown(VK_RETURN);
                input.KeyUp(VK_RETURN);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行关机失败");
            throw;
        }

        if (Settings.NotifyOnExecute)
            IAppHost.Host?.Services.GetServices<IHostedService>().OfType<SystemToolsNotificationProvider>()
                .FirstOrDefault()?.ShowNotification(new NotificationRequest
                {
                    MaskContent = NotificationContent.CreateTwoIconsMask("已执行计时关机", FluentIcons.IotAlertRegular, "")
                });

        await base.OnInvoke();
    }
}

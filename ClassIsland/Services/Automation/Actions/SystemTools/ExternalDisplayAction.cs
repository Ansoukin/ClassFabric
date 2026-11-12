using System;
using System.Diagnostics;
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

namespace ClassIsland.Services.Automation.Actions.SystemTools;

/// <summary>
/// 仅第二屏幕。靠 DisplaySwitch.exe 完成，它是 Windows 自带组件，其他平台直接跳过。
/// </summary>
[AutomationCategory("显示设置")]
[ActionInfo("SystemTools.ExternalDisplay", "仅第二屏幕", FluentIcons.DesktopMacRegular, false)]
public class ExternalDisplayAction(ILogger<ExternalDisplayAction> logger) : ActionBase<ShortcutKeyNotificationSettings>
{
    private readonly ILogger<ExternalDisplayAction> _logger = logger;

    protected override async Task OnInvoke()
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogWarning("DisplaySwitch.exe 仅在 Windows 上可用，跳过仅第二屏幕。");
            await base.OnInvoke();
            return;
        }

        try
        {
            _logger.LogInformation("正在执行仅第二屏幕命令");

            var processInfo = new ProcessStartInfo
            {
                FileName = "DisplaySwitch.exe",
                Arguments = "/external",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(processInfo);
            if (process != null)
            {
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                _logger.LogInformation("仅第二屏幕命令已执行，退出码: {ExitCode}", process.ExitCode);

                if (!string.IsNullOrEmpty(error))
                    _logger.LogWarning("错误输出: {Error}", error);
            }
            else
            {
                throw new Exception("无法启动 DisplaySwitch.exe 进程");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "仅第二屏幕失败");
            throw;
        }

        if (Settings.NotifyOnExecute)
            IAppHost.Host?.Services.GetServices<IHostedService>().OfType<SystemToolsNotificationProvider>()
                .FirstOrDefault()?.ShowNotification(new NotificationRequest
                {
                    MaskContent = NotificationContent.CreateTwoIconsMask("已执行仅第二屏幕操作", FluentIcons.IotAlertRegular, "")
                });

        await base.OnInvoke();
    }
}

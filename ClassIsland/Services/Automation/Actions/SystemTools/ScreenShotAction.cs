using System;
using System.IO;
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

[AutomationCategory("实用工具")]
[ActionInfo("SystemTools.ScreenShot", "屏幕截图", FluentIcons.ScreenCutRegular, false)]
public class ScreenShotAction(ILogger<ScreenShotAction> logger) : ActionBase<ScreenShotSettings>
{
    private readonly ILogger<ScreenShotAction> _logger = logger;

    static SystemToolsNotificationProvider? NotificationProvider =>
        IAppHost.Host?.Services.GetServices<IHostedService>()
            .OfType<SystemToolsNotificationProvider>().FirstOrDefault();

    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        if (string.IsNullOrWhiteSpace(Settings.SaveFolder))
        {
            _logger.LogWarning("保存路径为空");
            return;
        }

        try
        {
            if (!Directory.Exists(Settings.SaveFolder))
            {
                _logger.LogInformation("创建保存目录: {Dir}", Settings.SaveFolder);
                Directory.CreateDirectory(Settings.SaveFolder);
            }

            string fileName = $"屏幕截图{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.png";
            string fullPath = Path.Combine(Settings.SaveFolder, fileName);

            // WinForms Graphics.CopyFromScreen 的抓屏细节由平台实现层处理；不支持的平台返回 false。
            if (!PlatformServices.SystemEnvironmentService.CaptureScreenToPng(fullPath))
            {
                _logger.LogError("屏幕截图失败: {Path}", fullPath);
                throw new InvalidOperationException("屏幕截图失败，当前平台可能不支持抓屏。");
            }

            _logger.LogInformation("屏幕截图已保存: {FileName}", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "屏幕截图失败");
            throw;
        }

        if (Settings.NotifyOnExecute)
        {
            NotificationProvider?.ShowNotification(new NotificationRequest
            {
                MaskContent = NotificationContent.CreateTwoIconsMask("已自动执行屏幕截图", FluentIcons.IotAlertRegular, "")
            });
        }

        _logger.LogDebug("ScreenShotAction OnInvoke 完成");
    }
}

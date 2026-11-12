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

[AutomationCategory("实用工具")]
[ActionInfo("SystemTools.KillProcess", "退出进程", FluentIcons.ArrowExitFilled, false)]
public class KillProcessAction(ILogger<KillProcessAction> logger) : ActionBase<KillProcessSettings>
{
    private readonly ILogger<KillProcessAction> _logger = logger;

    static SystemToolsNotificationProvider? NotificationProvider =>
        IAppHost.Host?.Services.GetServices<IHostedService>()
            .OfType<SystemToolsNotificationProvider>().FirstOrDefault();

    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        if (Settings == null || string.IsNullOrWhiteSpace(Settings.ProcessName))
        {
            _logger.LogWarning("进程名为空");
            return;
        }

        // Process.GetProcessesByName/Kill 是 .NET 跨平台 API，不用 taskkill，非 Windows 桌面也能跑。
        var processName = Settings.ProcessName.Trim();
        if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            processName = processName[..^4];
        }

        _logger.LogInformation("正在终止进程: {ProcessName}", processName);
        Process[] pses;
        try
        {
            pses = Process.GetProcessesByName(processName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取相关联的进程列表失败");
            throw;
        }

        foreach (var ps in pses)
        {
            try
            {
                ps.Kill();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "终止进程 {ProcessName} (PID: {PID}) 失败", ps.ProcessName, ps.Id);
                continue;
            }
        }

        if (Settings.NotifyOnExecute)
        {
            NotificationProvider?.ShowNotification(new NotificationRequest
            {
                MaskContent = NotificationContent.CreateTwoIconsMask("已执行退出进程操作", FluentIcons.IotAlertRegular, "")
            });
        }

        _logger.LogDebug("KillProcessAction OnInvoke 完成");
    }
}

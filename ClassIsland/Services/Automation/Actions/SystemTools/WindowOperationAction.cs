using System;
using System.Linq;
using System.Threading.Tasks;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Icons;
using ClassIsland.Core.Models.Notification;
using ClassIsland.Models.Actions.SystemTools;
using ClassIsland.Platforms.Abstraction;
using ClassIsland.Platforms.Abstraction.Services;
using ClassIsland.Services.NotificationProviders;
using ClassIsland.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services.Automation.Actions.SystemTools;

/// <summary>
/// 「窗口操作」行动：对前台窗口执行最大化/最小化/向下还原/关闭。
/// </summary>
[AutomationCategory("模拟操作")]
[ActionInfo("SystemTools.WindowOperation", "窗口操作", FluentIcons.WindowMultipleSwapRegular, false)]
public class WindowOperationAction(ILogger<WindowOperationAction> logger) : ActionBase<WindowOperationSettings>
{
    private readonly ILogger<WindowOperationAction> _logger = logger;

    static SystemToolsNotificationProvider? NotificationProvider { get; } =
        IAppHost.Host.Services.GetServices<IHostedService>().OfType<SystemToolsNotificationProvider>().FirstOrDefault();

    protected override async Task OnInvoke()
    {
        _logger.LogDebug("WindowOperationAction OnInvoke 开始");

        try
        {
            // 平台服务在非 Windows 桌面端是空操作并返回 false，这里早退，避免误报"已切换窗口样式"。
            if (!OperatingSystem.IsWindows())
            {
                _logger.LogWarning("当前平台不支持窗口操作");
                return;
            }

            _logger.LogInformation("正在对前台窗口执行操作：{Operation}", Settings.Operation);

            var environment = PlatformServices.SystemEnvironmentService;
            ForegroundWindowOperation? operation = Settings.Operation switch
            {
                "最大化" => ForegroundWindowOperation.Maximize,
                "最小化" => ForegroundWindowOperation.Minimize,
                "向下还原" => ForegroundWindowOperation.Restore,
                "关闭窗口" => ForegroundWindowOperation.Close,
                _ => (ForegroundWindowOperation?)null
            };

            if (operation == null)
            {
                _logger.LogWarning("未知的窗口操作：{Operation}", Settings.Operation);
                return;
            }

            if (!environment.OperateForegroundWindow(operation.Value))
            {
                _logger.LogWarning("窗口操作未生效：{Operation}", Settings.Operation);
            }
            else
            {
                _logger.LogInformation("窗口操作已执行");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "窗口操作失败");
            throw;
        }
        if (Settings.NotifyOnExecute)
            NotificationProvider?.ShowNotification(new NotificationRequest
            {
                MaskContent = NotificationContent.CreateTwoIconsMask("已自动切换窗口样式", FluentIcons.IotAlertRegular, "")
            });


        await base.OnInvoke();
        _logger.LogDebug("WindowOperationAction OnInvoke 完成");
    }
}

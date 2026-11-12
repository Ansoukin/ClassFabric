using System;
using System.Collections.Generic;
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
/// 「模拟组合键」行动：全部按键按下后再逆序释放，模拟同时按下的效果。
/// </summary>
[AutomationCategory("模拟操作")]
[ActionInfo("SystemTools.SimulateKeyCombination", "模拟组合键", FluentIcons.KeyboardLayoutSplitRegular, false)]
public class SimulateKeyCombinationAction(ILogger<SimulateKeyCombinationAction> logger) : ActionBase<KeyCombinationSettings>
{
    private readonly ILogger<SimulateKeyCombinationAction> _logger = logger;
    private const int KeyEventDelay = 20;
    private const int KeyHoldDelay = 40;
    private const int MaxKeyCount = 5;

    static SystemToolsNotificationProvider? NotificationProvider { get; } =
        IAppHost.Host.Services.GetServices<IHostedService>().OfType<SystemToolsNotificationProvider>().FirstOrDefault();

    protected override async Task OnInvoke()
    {
        _logger.LogDebug("SimulateKeyCombinationAction OnInvoke 开始");

        var keys = Settings?.Keys
            .Where(x => x.KeyCode is > 0)
            .Take(MaxKeyCount)
            .Select(x => x.KeyCode!.Value)
            .ToList() ?? [];

        if (keys.Count == 0)
        {
            _logger.LogWarning("没有录入的组合键按键");
            return;
        }

        // 平台服务在非 Windows 桌面端是空操作，这里直接早退，省得重放空转还误报"已完成"。
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogWarning("当前平台不支持模拟组合键");
            return;
        }

        var pressedKeys = new List<byte>();
        try
        {
            _logger.LogInformation("正在模拟同时按下 {Count} 个按键", keys.Count);

            foreach (var keyCode in keys)
            {
                PlatformServices.SystemInputService.KeyDown(keyCode);
                pressedKeys.Add(keyCode);
                await Task.Delay(KeyEventDelay);
            }

            await Task.Delay(KeyHoldDelay);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "模拟组合键失败");
            throw;
        }
        finally
        {
            // 逆序释放，保证修饰键先于普通键抬起。
            for (var i = pressedKeys.Count - 1; i >= 0; i--)
            {
                PlatformServices.SystemInputService.KeyUp(pressedKeys[i]);
                await Task.Delay(KeyEventDelay);
            }
        }

        if (Settings?.NotifyOnExecute == true)
        {
            NotificationProvider?.ShowNotification(new NotificationRequest
            {
                MaskContent = NotificationContent.CreateTwoIconsMask("已完成模拟组合键", FluentIcons.IotAlertRegular, "")
            });
        }

        await base.OnInvoke();
        _logger.LogDebug("SimulateKeyCombinationAction OnInvoke 完成");
    }
}

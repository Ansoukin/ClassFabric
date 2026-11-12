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
/// 「模拟键盘」行动：重放录制的键盘操作序列。
/// </summary>
[AutomationCategory("模拟操作")]
[ActionInfo("SystemTools.SimulateKeyboard", "模拟键盘", FluentIcons.KeyboardLayoutFloatRegular, false)]
public class SimulateKeyboardAction(ILogger<SimulateKeyboardAction> logger) : ActionBase<KeyboardInputSettings>
{
    private readonly ILogger<SimulateKeyboardAction> _logger = logger;
    private const int KeyPressDelay = 20;
    private const int KeyIntervalDelay = 100;

    static SystemToolsNotificationProvider? NotificationProvider { get; } =
        IAppHost.Host.Services.GetServices<IHostedService>().OfType<SystemToolsNotificationProvider>().FirstOrDefault();

    protected override async Task OnInvoke()
    {
        _logger.LogDebug("SimulateKeyboardAction OnInvoke 开始");

        if (Settings == null)
        {
            _logger.LogWarning("没有录制的按键");
            return;
        }

        var actions = Settings.Actions.Count > 0 ? Settings.Actions : ConvertLegacyKeys(Settings.Keys);
        if (actions.Count == 0)
        {
            _logger.LogWarning("没有录制的按键");
            return;
        }

        // 平台服务在非 Windows 桌面端是空操作，这里直接早退，省得重放空转还误报"已完成"。
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogWarning("当前平台不支持模拟键盘输入");
            return;
        }

        var pressedKeys = new HashSet<byte>();
        var cancellationToken = InterruptCancellationToken;
        try
        {
            _logger.LogInformation("正在模拟 {Count} 个按键操作", actions.Count);

            for (int i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                await Task.Delay(checked((int)action.Interval), cancellationToken);

                switch (action.Type)
                {
                    case KeyboardAction.ActionType.KeyDown:
                        // 先记账再发送：按下失败时也要兜底释放，避免修饰键卡死。
                        pressedKeys.Add(action.KeyCode);
                        PlatformServices.SystemInputService.KeyDown(action.KeyCode);
                        break;
                    case KeyboardAction.ActionType.KeyUp:
                        PlatformServices.SystemInputService.KeyUp(action.KeyCode);
                        pressedKeys.Remove(action.KeyCode);
                        break;
                    default:
                        pressedKeys.Add(action.KeyCode);
                        PlatformServices.SystemInputService.KeyDown(action.KeyCode);
                        await Task.Delay(KeyPressDelay, cancellationToken);
                        PlatformServices.SystemInputService.KeyUp(action.KeyCode);
                        pressedKeys.Remove(action.KeyCode);
                        break;
                }
            }

            _logger.LogInformation("按键模拟完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "模拟键盘失败");
            throw;
        }
        finally
        {
            foreach (var keyCode in pressedKeys)
            {
                try
                {
                    PlatformServices.SystemInputService.KeyUp(keyCode);
                }
                catch (Exception releaseException)
                {
                    _logger.LogWarning(releaseException, "释放模拟按键失败，键码: {KeyCode}", keyCode);
                }
            }

            pressedKeys.Clear();
        }
        if (Settings.NotifyOnExecute)
            NotificationProvider?.ShowNotification(new NotificationRequest
            {
                MaskContent = NotificationContent.CreateTwoIconsMask("已完成模拟键盘输入", FluentIcons.IotAlertRegular, "")
            });


        await base.OnInvoke();
        _logger.LogDebug("SimulateKeyboardAction OnInvoke 完成");
    }

    /// <summary>
    /// 把旧版 "键码:按键名" 列表转成操作序列。旧数据只有单击，间隔统一取固定值。
    /// </summary>
    private static List<KeyboardAction> ConvertLegacyKeys(List<string>? keys)
    {
        var actions = new List<KeyboardAction>();
        if (keys == null)
        {
            return actions;
        }

        foreach (var key in keys)
        {
            var parts = key.Split(':', 2);
            if (!byte.TryParse(parts[0], out var keyCode))
            {
                continue;
            }

            actions.Add(new KeyboardAction
            {
                Type = KeyboardAction.ActionType.Press,
                KeyCode = keyCode,
                KeyName = parts.Length > 1 ? parts[1] : keyCode.ToString(),
                Interval = actions.Count == 0 ? 0 : KeyIntervalDelay
            });
        }

        return actions;
    }
}

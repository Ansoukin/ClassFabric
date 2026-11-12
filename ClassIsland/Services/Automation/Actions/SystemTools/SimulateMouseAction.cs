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
/// 「模拟鼠标」行动：重放录制的鼠标操作序列（点击/滚动/拖动）。
/// </summary>
[AutomationCategory("模拟操作")]
[ActionInfo("SystemTools.SimulateMouse", "模拟鼠标", FluentIcons.CursorClickRegular, false)]
public class SimulateMouseAction(ILogger<SimulateMouseAction> logger) : ActionBase<MouseInputSettings>
{
    private readonly ILogger<SimulateMouseAction> _logger = logger;
    private const int MouseDelay = 20;

    static SystemToolsNotificationProvider? NotificationProvider { get; } =
        IAppHost.Host.Services.GetServices<IHostedService>().OfType<SystemToolsNotificationProvider>().FirstOrDefault();

    protected override async Task OnInvoke()
    {
        _logger.LogDebug("SimulateMouseAction OnInvoke 开始");
        var isLeftButtonDown = false;
        var isRightButtonDown = false;
        var isMiddleButtonDown = false;
        var lastMouseX = 0;
        var lastMouseY = 0;
        var cancellationToken = InterruptCancellationToken;

        if (Settings == null || Settings.Actions == null || Settings.Actions.Count == 0)
        {
            _logger.LogWarning("没有录制的鼠标操作");
            return;
        }

        // 平台服务在非 Windows 桌面端是空操作，这里直接早退，省得重放空转还误报"已完成"。
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogWarning("当前平台不支持模拟鼠标操作");
            return;
        }

        var input = PlatformServices.SystemInputService;
        try
        {
            _logger.LogInformation("正在模拟 {Count} 个鼠标操作", Settings.Actions.Count);

            for (int i = 0; i < Settings.Actions.Count; i++)
            {
                var action = Settings.Actions[i];
                lastMouseX = action.X;
                lastMouseY = action.Y;

                await Task.Delay(checked((int)action.Interval), cancellationToken);

                switch (action.Type)
                {
                    case MouseAction.ActionType.LeftClick:
                        if (isLeftButtonDown)
                        {
                            input.MouseUp(SystemMouseButton.Left);
                            isLeftButtonDown = false;
                            await Task.Delay(MouseDelay, cancellationToken);
                        }

                        input.MoveMouseCursor(action.X, action.Y);
                        await Task.Delay(MouseDelay, cancellationToken);
                        isLeftButtonDown = true;
                        input.MouseDown(SystemMouseButton.Left);
                        await Task.Delay(MouseDelay, cancellationToken);
                        input.MouseUp(SystemMouseButton.Left);
                        isLeftButtonDown = false;
                        break;

                    case MouseAction.ActionType.RightClick:
                        // 右键/中键操作前先抬起还按着的左键，避免拖动状态串场。
                        if (isLeftButtonDown)
                        {
                            input.MouseUp(SystemMouseButton.Left);
                            isLeftButtonDown = false;
                            await Task.Delay(MouseDelay, cancellationToken);
                        }

                        input.MoveMouseCursor(action.X, action.Y);
                        await Task.Delay(MouseDelay, cancellationToken);
                        isRightButtonDown = true;
                        input.MouseDown(SystemMouseButton.Right);
                        await Task.Delay(MouseDelay, cancellationToken);
                        input.MouseUp(SystemMouseButton.Right);
                        isRightButtonDown = false;
                        break;

                    case MouseAction.ActionType.MiddleClick:
                        if (isLeftButtonDown)
                        {
                            input.MouseUp(SystemMouseButton.Left);
                            isLeftButtonDown = false;
                            await Task.Delay(MouseDelay, cancellationToken);
                        }

                        input.MoveMouseCursor(action.X, action.Y);
                        await Task.Delay(MouseDelay, cancellationToken);
                        isMiddleButtonDown = true;
                        input.MouseDown(SystemMouseButton.Middle);
                        await Task.Delay(MouseDelay, cancellationToken);
                        input.MouseUp(SystemMouseButton.Middle);
                        isMiddleButtonDown = false;
                        break;

                    case MouseAction.ActionType.Scroll:
                        if (isLeftButtonDown)
                        {
                            input.MouseUp(SystemMouseButton.Left);
                            isLeftButtonDown = false;
                            await Task.Delay(MouseDelay, cancellationToken);
                        }

                        input.MoveMouseCursor(action.X, action.Y);
                        await Task.Delay(MouseDelay, cancellationToken);
                        input.MouseWheel(action.ScrollDelta);
                        break;

                    case MouseAction.ActionType.DragMove:
                        if (!isLeftButtonDown)
                        {
                            input.MoveMouseCursor(action.X, action.Y);
                            await Task.Delay(MouseDelay, cancellationToken);
                            isLeftButtonDown = true;
                            input.MouseDown(SystemMouseButton.Left);
                        }
                        else
                        {
                            input.MoveMouseCursor(action.X, action.Y);
                            await Task.Delay(MouseDelay, cancellationToken);
                        }

                        if (action.IsDragEnd && isLeftButtonDown)
                        {
                            input.MouseUp(SystemMouseButton.Left);
                            isLeftButtonDown = false;
                        }

                        break;
                }
            }

            if (isLeftButtonDown)
            {
                // 序列以拖动中状态结尾时补一次抬起，否则左键会一直卡在按住状态。
                input.MouseUp(SystemMouseButton.Left);
                isLeftButtonDown = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "模拟鼠标失败");
            throw;
        }
        finally
        {
            // 失败/中断可能发生在按下与抬起之间，结束前要把这次调用中所有按住的键释放掉。
            if (isLeftButtonDown)
            {
                try
                {
                    input.MouseUp(SystemMouseButton.Left);
                }
                catch (Exception releaseException)
                {
                    _logger.LogWarning(releaseException, "释放模拟鼠标左键失败");
                }
                isLeftButtonDown = false;
            }

            if (isRightButtonDown)
            {
                try
                {
                    input.MouseUp(SystemMouseButton.Right);
                }
                catch (Exception releaseException)
                {
                    _logger.LogWarning(releaseException, "释放模拟鼠标右键失败");
                }
                isRightButtonDown = false;
            }

            if (isMiddleButtonDown)
            {
                try
                {
                    input.MouseUp(SystemMouseButton.Middle);
                }
                catch (Exception releaseException)
                {
                    _logger.LogWarning(releaseException, "释放模拟鼠标中键失败");
                }
                isMiddleButtonDown = false;
            }
        }
        if (Settings.NotifyOnExecute)
            NotificationProvider?.ShowNotification(new NotificationRequest
            {
                MaskContent = NotificationContent.CreateTwoIconsMask("已结束自动操作自动化", FluentIcons.IotAlertRegular, "")
            });


        await base.OnInvoke();
        _logger.LogDebug("SimulateMouseAction OnInvoke 完成");
    }
}

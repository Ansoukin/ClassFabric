using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Icons;
using ClassIsland.Core.Models.Notification;
using ClassIsland.Models.Actions.SystemTools;
using ClassIsland.Services.NotificationProviders;
using ClassIsland.Services.SystemTools;
using ClassIsland.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System.Linq;
namespace ClassIsland.Services.Automation.Actions.SystemTools;

/// <summary>
/// 显示/隐藏悬浮窗。可恢复：执行时记下执行前的显示状态，恢复时写回。
/// </summary>
[AutomationCategory("应用生命周期")]
[ActionInfo("SystemTools.ShowFloatingWindow", "显示悬浮窗", FluentIcons.LayerDiagonalSparkleRegular, false)]
public class ShowFloatingWindowAction(
    ILogger<ShowFloatingWindowAction> logger,
    FloatingWindowService floatingWindowService,
    SettingsService settingsService) : ActionBase<ShowFloatingWindowSettings>
{
    private readonly ILogger<ShowFloatingWindowAction> _logger = logger;
    private readonly FloatingWindowService _floatingWindowService = floatingWindowService;
    private readonly SettingsService _settingsService = settingsService;
    private static readonly ConcurrentDictionary<Guid, bool> PreviousStates = new();

    static SystemToolsNotificationProvider? NotificationProvider =>
        IAppHost.Host?.Services.GetServices<IHostedService>()
            .OfType<SystemToolsNotificationProvider>().FirstOrDefault();

    protected override async Task OnInvoke()
    {
        _logger.LogDebug("ShowFloatingWindowAction OnInvoke 开始");

        try
        {
            var shouldShow = Settings.ShowFloatingWindow;
            var settings = _settingsService.Settings;

            // 没有任何悬浮窗按钮注册时，悬浮窗无内容可显，强制隐藏
            if (_floatingWindowService.Entries.Count == 0)
            {
                shouldShow = false;
                _logger.LogDebug("没有可用的悬浮窗组件，强制隐藏悬浮窗");
            }

            if (IsRevertable)
            {
                PreviousStates[ActionSet.Guid] = settings.ShowFloatingWindow;
            }

            settings.ShowFloatingWindow = shouldShow;
            _settingsService.SaveSettings("行动切换悬浮窗显示状态");

            _floatingWindowService.UpdateWindowState();

            _logger.LogInformation("悬浮窗状态已更新为: {State}", shouldShow ? "开启" : "关闭");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新悬浮窗状态失败");
            throw;
        }

        if (Settings.NotifyOnExecute)
        {
            NotificationProvider?.ShowNotification(new NotificationRequest
            {
                MaskContent = NotificationContent.CreateTwoIconsMask("已自动显示悬浮窗", FluentIcons.IotAlertRegular, "")
            });
        }

        await base.OnInvoke();
        _logger.LogDebug("ShowFloatingWindowAction OnInvoke 完成");
    }

    protected override async Task OnRevert()
    {
        await base.OnRevert();

        if (!PreviousStates.TryRemove(ActionSet.Guid, out var previousState))
        {
            _logger.LogInformation("未找到恢复快照，跳过悬浮窗恢复。ActionSet={ActionSetGuid}", ActionSet.Guid);
            return;
        }

        var settings = _settingsService.Settings;
        settings.ShowFloatingWindow = previousState;
        _settingsService.SaveSettings("行动恢复悬浮窗显示状态");
        _floatingWindowService.UpdateWindowState();

        _logger.LogInformation("已恢复悬浮窗状态为: {State}", previousState ? "开启" : "关闭");
    }
}

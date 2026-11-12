using System;
using System.Collections.Concurrent;
using System.Linq;
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

namespace ClassIsland.Services.Automation.Actions.SystemTools;

/// <summary>
/// 切换悬浮窗层级。可恢复：执行前记下当前层级，恢复时写回。
/// </summary>
[AutomationCategory("应用生命周期")]
[ActionInfo("SystemTools.ToggleFloatingWindowLayer", "切换悬浮窗层级", FluentIcons.HourglassRegular, false)]
public class ToggleFloatingWindowLayerAction(
    ILogger<ToggleFloatingWindowLayerAction> logger,
    FloatingWindowService floatingWindowService,
    SettingsService settingsService) : ActionBase<ToggleFloatingWindowLayerSettings>
{
    private readonly ILogger<ToggleFloatingWindowLayerAction> _logger = logger;
    private readonly FloatingWindowService _floatingWindowService = floatingWindowService;
    private readonly SettingsService _settingsService = settingsService;
    private static readonly ConcurrentDictionary<Guid, int> PreviousLayers = new();

    static SystemToolsNotificationProvider? NotificationProvider =>
        IAppHost.Host?.Services.GetServices<IHostedService>()
            .OfType<SystemToolsNotificationProvider>().FirstOrDefault();

    protected override async Task OnInvoke()
    {
        _logger.LogDebug("ToggleFloatingWindowLayerAction OnInvoke 开始");

        try
        {
            // TargetLayer: -1=切换, 0=置底, 1=置顶
            if (IsRevertable)
            {
                PreviousLayers[ActionSet.Guid] = _settingsService.Settings.FloatingWindowLayer;
            }

            if (Settings.TargetLayer >= 0)
            {
                _floatingWindowService.SetWindowLayer(Settings.TargetLayer);
                _logger.LogInformation("已设置悬浮窗层级为: {Layer}", Settings.TargetLayer == 0 ? "置底" : "置顶");
            }
            else
            {
                _floatingWindowService.ToggleWindowLayer();
                _logger.LogInformation("已切换悬浮窗层级状态");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换悬浮窗层级失败");
            throw;
        }

        if (Settings.NotifyOnExecute)
        {
            NotificationProvider?.ShowNotification(new NotificationRequest
            {
                MaskContent = NotificationContent.CreateTwoIconsMask("已自动切换悬浮窗层级", FluentIcons.IotAlertRegular, "")
            });
        }

        await base.OnInvoke();
        _logger.LogDebug("ToggleFloatingWindowLayerAction OnInvoke 完成");
    }

    protected override async Task OnRevert()
    {
        await base.OnRevert();

        if (!PreviousLayers.TryRemove(ActionSet.Guid, out var previousLayer))
        {
            _logger.LogInformation("未找到层级恢复快照，跳过悬浮窗层级恢复。ActionSet={ActionSetGuid}", ActionSet.Guid);
            return;
        }

        try
        {
            _floatingWindowService.SetWindowLayer(previousLayer);
            _logger.LogInformation("已恢复悬浮窗层级为: {Layer}", previousLayer == 0 ? "置底" : "置顶");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "恢复悬浮窗层级失败");
            throw;
        }
    }
}

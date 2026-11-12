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
/// 切换悬浮窗配置方案。可恢复：执行前记下当前方案名，恢复时切回。
/// </summary>
[AutomationCategory("应用生命周期")]
[ActionInfo("SystemTools.ToggleFloatingWindowProfile", "切换悬浮窗配置方案", FluentIcons.HourglassRegular, false)]
public class ToggleFloatingWindowProfileAction(
    ILogger<ToggleFloatingWindowProfileAction> logger,
    FloatingWindowService floatingWindowService) : ActionBase<ToggleFloatingWindowProfileSettings>
{
    private readonly ILogger<ToggleFloatingWindowProfileAction> _logger = logger;
    private readonly FloatingWindowService _floatingWindowService = floatingWindowService;
    private static readonly ConcurrentDictionary<Guid, string> PreviousProfiles = new();

    static SystemToolsNotificationProvider? NotificationProvider =>
        IAppHost.Host?.Services.GetServices<IHostedService>()
            .OfType<SystemToolsNotificationProvider>().FirstOrDefault();

    protected override async Task OnInvoke()
    {
        _logger.LogDebug("ToggleFloatingWindowProfileAction OnInvoke 开始");

        try
        {
            var currentProfileName = _floatingWindowService.ProfileManager.CurrentProfileName;

            // TargetProfileName: null=切换到下一个, 其他=指定方案名称
            if (IsRevertable)
            {
                PreviousProfiles[ActionSet.Guid] = currentProfileName;
            }

            if (!string.IsNullOrWhiteSpace(Settings.TargetProfileName))
            {
                _floatingWindowService.SwitchToProfile(Settings.TargetProfileName);
                _logger.LogInformation("已切换到悬浮窗配置方案: {Name}", Settings.TargetProfileName);
            }
            else
            {
                _floatingWindowService.ToggleWindowProfile();
                _logger.LogInformation("已切换到下一个悬浮窗配置方案");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换悬浮窗配置方案失败");
            throw;
        }

        if (Settings.NotifyOnExecute)
        {
            NotificationProvider?.ShowNotification(new NotificationRequest
            {
                MaskContent = NotificationContent.CreateTwoIconsMask("已自动切换悬浮窗配置方案", FluentIcons.IotAlertRegular, "")
            });
        }

        await base.OnInvoke();
        _logger.LogDebug("ToggleFloatingWindowProfileAction OnInvoke 完成");
    }

    protected override async Task OnRevert()
    {
        await base.OnRevert();

        if (!PreviousProfiles.TryRemove(ActionSet.Guid, out var previousProfile))
        {
            _logger.LogInformation("未找到配置方案恢复快照，跳过悬浮窗配置方案恢复。ActionSet={ActionSetGuid}", ActionSet.Guid);
            return;
        }

        try
        {
            _floatingWindowService.SwitchToProfile(previousProfile);
            _logger.LogInformation("已恢复悬浮窗配置方案为: {Name}", previousProfile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "恢复悬浮窗配置方案失败");
            throw;
        }
    }
}

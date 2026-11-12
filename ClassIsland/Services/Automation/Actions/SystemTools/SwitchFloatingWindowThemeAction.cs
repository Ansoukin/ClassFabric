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
/// 切换悬浮窗主题。可恢复：执行前记下当前主题值，恢复时写回。
/// </summary>
[AutomationCategory("应用生命周期")]
[ActionInfo("SystemTools.SwitchFloatingWindowTheme", "切换悬浮窗主题", FluentIcons.DrinkWineFilled, false)]
public class SwitchFloatingWindowThemeAction(
    ILogger<SwitchFloatingWindowThemeAction> logger,
    FloatingWindowService floatingWindowService,
    SettingsService settingsService) : ActionBase<SwitchFloatingWindowThemeSettings>
{
    private readonly ILogger<SwitchFloatingWindowThemeAction> _logger = logger;
    private readonly FloatingWindowService _floatingWindowService = floatingWindowService;
    private readonly SettingsService _settingsService = settingsService;
    private static readonly ConcurrentDictionary<Guid, int> PreviousThemes = new();

    static SystemToolsNotificationProvider? NotificationProvider =>
        IAppHost.Host?.Services.GetServices<IHostedService>()
            .OfType<SystemToolsNotificationProvider>().FirstOrDefault();

    protected override async Task OnInvoke()
    {
        _logger.LogDebug("SwitchFloatingWindowThemeAction OnInvoke 开始");

        try
        {
            // TargetTheme: -1=切换到下一个, 0=跟随系统, 1=浅色, 2=深色, 3=自适应背景
            if (IsRevertable)
            {
                PreviousThemes[ActionSet.Guid] = _settingsService.Settings.FloatingWindowTheme;
            }

            if (Settings.TargetTheme >= 0)
            {
                _floatingWindowService.SetWindowTheme(Settings.TargetTheme);
                _logger.LogInformation("已设置悬浮窗主题为: {Theme}", GetThemeName(Settings.TargetTheme));
            }
            else
            {
                _floatingWindowService.ToggleWindowTheme();
                _logger.LogInformation("已切换到下一个悬浮窗主题");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换悬浮窗主题失败");
            throw;
        }

        if (Settings.NotifyOnExecute)
        {
            NotificationProvider?.ShowNotification(new NotificationRequest
            {
                MaskContent = NotificationContent.CreateTwoIconsMask("已自动切换悬浮窗主题", FluentIcons.IotAlertRegular, "")
            });
        }

        await base.OnInvoke();
        _logger.LogDebug("SwitchFloatingWindowThemeAction OnInvoke 完成");
    }

    protected override async Task OnRevert()
    {
        await base.OnRevert();

        if (!PreviousThemes.TryRemove(ActionSet.Guid, out var previousTheme))
        {
            _logger.LogInformation("未找到主题恢复快照，跳过悬浮窗主题恢复。ActionSet={ActionSetGuid}", ActionSet.Guid);
            return;
        }

        try
        {
            _floatingWindowService.SetWindowTheme(previousTheme);
            _logger.LogInformation("已恢复悬浮窗主题为: {Theme}", GetThemeName(previousTheme));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "恢复悬浮窗主题失败");
            throw;
        }
    }

    private static string GetThemeName(int theme)
    {
        return theme switch
        {
            0 => "跟随系统",
            1 => "浅色",
            2 => "深色",
            3 => "自适应背景",
            _ => "未知"
        };
    }
}

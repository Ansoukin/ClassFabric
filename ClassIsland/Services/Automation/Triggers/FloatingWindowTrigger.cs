using System;
using System.ComponentModel;
using System.Linq;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Icons;
using ClassIsland.Models.Automation.Triggers;
using ClassIsland.Services.SystemTools;
using ClassIsland.Shared.Enums;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services.Automation.Triggers;

/// <summary>
/// 从悬浮窗触发。触发器把自己注册给 FloatingWindowService，由悬浮窗按钮的点按事件回调 TriggerFromFloatingWindow。
/// 按钮 ID/图标/名称变化时要重新 RegisterTrigger，悬浮窗才会按新数据重排按钮。
/// </summary>
[AutomationCategory("应用生命周期")]
[TriggerInfo("SystemTools.FloatingWindowTrigger", "从悬浮窗触发", FluentIcons.LayerDiagonalSparkleRegular)]
public class FloatingWindowTrigger(
    FloatingWindowService floatingWindowService,
    ILogger<FloatingWindowTrigger> logger) : TriggerBase<FloatingWindowTriggerSettings>
{
    private readonly FloatingWindowService _floatingWindowService = floatingWindowService;
    private readonly ILogger<FloatingWindowTrigger> _logger = logger;
    private bool _isNormalizingButtonId;

    public override void Loaded()
    {
        Settings.PropertyChanged += OnSettingsChanged;
        if (AssociatedWorkflow?.ActionSet != null)
        {
            AssociatedWorkflow.ActionSet.PropertyChanged += OnActionSetPropertyChanged;
        }
        EnsureButtonId();
        _floatingWindowService.RegisterTrigger(this);
    }

    public override void UnLoaded()
    {
        Settings.PropertyChanged -= OnSettingsChanged;
        if (AssociatedWorkflow?.ActionSet != null)
        {
            AssociatedWorkflow.ActionSet.PropertyChanged -= OnActionSetPropertyChanged;
        }
        _floatingWindowService.UnregisterTrigger(this);
    }

    public void TriggerFromFloatingWindow()
    {
        var actionSet = AssociatedWorkflow?.ActionSet;
        var useRevertMode = actionSet?.IsRevertEnabled == true;
        var isOn = actionSet?.Status == ActionSetStatus.IsOn;

        _logger.LogInformation("从悬浮窗触发触发器: {ButtonId}, 启用恢复: {UseRevertMode}, 当前状态: {Status}",
            Settings.ButtonId, useRevertMode, actionSet?.Status);

        if (useRevertMode && isOn)
        {
            TriggerRevert();
            return;
        }

        Trigger();
    }

    public string GetButtonId()
    {
        EnsureButtonId();
        return Settings.ButtonId;
    }

    // 服务在启动时会用这个方法排查重复按钮 ID；分配新 ID 期间要屏蔽 PropertyChanged，
    // 否则每次赋值都会触发一次重新注册，白白重排 N 遍按钮。
    public string GetUniqueButtonId(Func<string, bool> isButtonIdInUse)
    {
        EnsureButtonId();
        if (!isButtonIdInUse(Settings.ButtonId))
        {
            return Settings.ButtonId;
        }

        _isNormalizingButtonId = true;
        try
        {
            do
            {
                Settings.ButtonId = Guid.NewGuid().ToString("N");
            } while (isButtonIdInUse(Settings.ButtonId));
        }
        finally
        {
            _isNormalizingButtonId = false;
        }

        _logger.LogInformation("检测到重复的悬浮窗按钮 ID，已为触发器生成新 ID: {ButtonId}", Settings.ButtonId);
        return Settings.ButtonId;
    }

    public string GetIcon()
    {
        return Settings.Icon;
    }

    // 按钮处于待恢复状态时，悬浮窗上直接显示「恢复」，编辑布局时仍显示原始名称
    public string GetButtonName()
    {
        return ShouldShowRevertButton() ? "恢复" : Settings.ButtonName;
    }

    public string GetLayoutButtonName()
    {
        return Settings.ButtonName;
    }

    public bool ShouldUseRevertStyle()
    {
        return ShouldShowRevertButton();
    }

    public bool IsRevertEnabled()
    {
        return AssociatedWorkflow?.ActionSet?.IsRevertEnabled == true;
    }

    // 悬浮窗右键「取消待恢复状态」：把行动组状态掰回关闭，且不触发恢复行动。
    // Status 属性的类型在各版本间有差异，走反射按枚举名找 IsOff/Off 值，找不到就放弃。
    public void CancelIsOnState()
    {
        var actionSet = AssociatedWorkflow?.ActionSet;
        if (actionSet == null || actionSet.Status != ActionSetStatus.IsOn)
        {
            return;
        }

        var statusProperty = actionSet.GetType().GetProperty("Status");
        if (statusProperty?.CanWrite != true || !statusProperty.PropertyType.IsEnum)
        {
            return;
        }

        var enumValues = Enum.GetValues(statusProperty.PropertyType).Cast<object>().ToList();
        var targetStatus = enumValues.FirstOrDefault(v =>
                               string.Equals(v.ToString(), "IsOff", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(v.ToString(), "Off", StringComparison.OrdinalIgnoreCase))
                           ?? enumValues.FirstOrDefault(v => !Equals(v, actionSet.Status));

        if (targetStatus == null)
        {
            return;
        }

        statusProperty.SetValue(actionSet, targetStatus);
        _logger.LogInformation("通过悬浮窗右键取消等待恢复状态: {ButtonId}, 原状态: {OldStatus}, 新状态: {NewStatus}",
            Settings.ButtonId, ActionSetStatus.IsOn, targetStatus);
        _floatingWindowService.RegisterTrigger(this);
    }

    private void EnsureButtonId()
    {
        if (string.IsNullOrWhiteSpace(Settings.ButtonId))
        {
            Settings.ButtonId = Guid.NewGuid().ToString("N");
        }
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isNormalizingButtonId)
        {
            return;
        }

        if (e.PropertyName == nameof(FloatingWindowTriggerSettings.Icon) ||
            e.PropertyName == nameof(FloatingWindowTriggerSettings.ButtonId) ||
            e.PropertyName == nameof(FloatingWindowTriggerSettings.ButtonName))
        {
            _floatingWindowService.RegisterTrigger(this);
        }
    }

    private void OnActionSetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Status" ||
            e.PropertyName == "IsRevertEnabled")
        {
            _floatingWindowService.RegisterTrigger(this);
        }
    }

    private bool ShouldShowRevertButton()
    {
        var actionSet = AssociatedWorkflow?.ActionSet;
        return actionSet?.IsRevertEnabled == true && actionSet.Status == ActionSetStatus.IsOn;
    }
}

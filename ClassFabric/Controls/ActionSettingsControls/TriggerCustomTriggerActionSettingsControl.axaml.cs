using System;
using ClassFabric.Core.Abstractions.Controls;
using ClassFabric.Models.Actions;

namespace ClassFabric.Controls.ActionSettingsControls;

public partial class TriggerCustomTriggerActionSettingsControl : ActionSettingsControlBase<TriggerCustomTriggerActionSettings>
{
    public TriggerCustomTriggerActionSettingsControl()
    {
        InitializeComponent();
        DataContext = this;
    }

    // Avalonia 内建转换不支持字符串与 Guid 互转，用字符串包装属性做解析中转。
    public string TargetTriggerIdText
    {
        get => SettingsInternal is not TriggerCustomTriggerActionSettings s
            ? string.Empty
            : s.TargetTriggerId.ToString();
        set
        {
            if (SettingsInternal is not TriggerCustomTriggerActionSettings s) return;
            if (string.IsNullOrWhiteSpace(value))
                s.TargetTriggerId = Guid.Empty;
            else if (Guid.TryParse(value.Trim(), out var id))
                s.TargetTriggerId = id;
        }
    }
}

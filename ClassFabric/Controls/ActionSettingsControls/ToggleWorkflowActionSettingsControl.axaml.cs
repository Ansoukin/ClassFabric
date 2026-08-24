using System;
using ClassFabric.Core.Abstractions.Controls;
using ClassFabric.Models.Actions;

namespace ClassFabric.Controls.ActionSettingsControls;

public partial class ToggleWorkflowActionSettingsControl : ActionSettingsControlBase<ToggleWorkflowActionSettings>
{
    public ToggleWorkflowActionSettingsControl()
    {
        InitializeComponent();
        DataContext = this;
    }

    // Avalonia 内建转换不支持字符串与 Guid 互转，用字符串包装属性做解析中转。
    public string TargetProfileIdText
    {
        get => SettingsInternal is not ToggleWorkflowActionSettings s
            ? string.Empty
            : s.TargetProfileId.ToString();
        set
        {
            if (SettingsInternal is not ToggleWorkflowActionSettings s) return;
            if (string.IsNullOrWhiteSpace(value))
                s.TargetProfileId = Guid.Empty;
            else if (Guid.TryParse(value.Trim(), out var id))
                s.TargetProfileId = id;
        }
    }
}

using System;
using ClassFabric.Core.Abstractions.Controls;
using ClassFabric.Models.Rules;
using ClassFabric.ViewModels;

namespace ClassFabric.Controls.RuleSettingsControls;

/// <summary>
/// UsingClassPlanRuleSettingsControl 的交互逻辑
/// </summary>
public partial class UsingClassPlanRuleSettingsControl : RuleSettingsControlBase<UsingClassPlanRuleSettings>
{
    public ProfileSettingsViewModel ProfileSettingsViewModel { get; }

    public UsingClassPlanRuleSettingsControl(ProfileSettingsViewModel vm)
    {
        ProfileSettingsViewModel = vm;
        InitializeComponent();
    }
}

using System;
using ClassFabric.Core.Abstractions.Controls;
using ClassFabric.Models.Rules;
using ClassFabric.ViewModels;

namespace ClassFabric.Controls.RuleSettingsControls;

/// <summary>
/// UsingTimeLayoutRuleSettingsControl 的交互逻辑
/// </summary>
public partial class UsingTimeLayoutRuleSettingsControl : RuleSettingsControlBase<UsingTimeLayoutRuleSettings>
{
    public ProfileSettingsViewModel ProfileSettingsViewModel { get; }

    public UsingTimeLayoutRuleSettingsControl(ProfileSettingsViewModel vm)
    {
        ProfileSettingsViewModel = vm;
        InitializeComponent();
    }
}

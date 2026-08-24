using System;
using ClassFabric.Core.Abstractions.Controls;
using ClassFabric.Core.Abstractions.Services;
using ClassFabric.Core.Abstractions.Services.Management;
using ClassFabric.Core.ComponentModels;
using ClassFabric.Models.Rules;
using ClassFabric.Services;
using ClassFabric.Shared.Models.Profile;
using ClassFabric.ViewModels;

namespace ClassFabric.Controls.RuleSettingsControls;

/// <summary>
/// CurrentSubjectRuleSettingsControl.xaml 的交互逻辑
/// </summary>
public partial class CurrentSubjectRuleSettingsControl : RuleSettingsControlBase<CurrentSubjectRuleSettings>
{
    public ProfileSettingsViewModel ProfileSettingsViewModel { get; }

    public CurrentSubjectRuleSettingsControl(ProfileSettingsViewModel vm)
    {
        ProfileSettingsViewModel = vm;
        InitializeComponent();
    }
}

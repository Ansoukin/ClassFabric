using ClassFabric.Core.Abstractions.Controls;
using ClassFabric.Core.Attributes;
using ClassFabric.Core.Enums;
using ClassFabric.Models.AttachedSettings;

namespace ClassFabric.Controls.AttachedSettingsControls;

/// <summary>
/// AfterSchoolNotificationAttachedSettingsControl.xaml 的交互逻辑
/// </summary>
[AttachedSettingsUsage(AttachedSettingsTargets.ClassPlan | AttachedSettingsTargets.TimeLayout)]
[AttachedSettingsControlInfo("8FBC3A26-6D20-44DD-B895-B9411E3DDC51", "放学提醒设置", "\ued34")]
public partial class AfterSchoolNotificationAttachedSettingsControl : AttachedSettingsControlBase<AfterSchoolNotificationAttachedSettings>
{
    public AfterSchoolNotificationAttachedSettingsControl()
    {
        InitializeComponent();
    }
}


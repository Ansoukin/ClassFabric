using Avalonia.Interactivity;
using ClassFabric.Core.Abstractions.Controls;
using ClassFabric.Core.Attributes;
using ClassFabric.Core.Enums;
using ClassFabric.Models.AttachedSettings;

namespace ClassFabric.Controls.AttachedSettingsControls;

/// <summary>
/// ClassNotificationAttachedSettingsControl.xaml 的交互逻辑
/// </summary>
[AttachedSettingsUsage(AttachedSettingsTargets.ClassPlan | AttachedSettingsTargets.TimeLayout |
                       AttachedSettingsTargets.Lesson | AttachedSettingsTargets.Subject |
                       AttachedSettingsTargets.TimePoint)]
[AttachedSettingsControlInfo("08F0D9C3-C770-4093-A3D0-02F3D90C24BC", "上课提醒设置", "")]
public partial class ClassNotificationAttachedSettingsControl : AttachedSettingsControlBase<ClassNotificationAttachedSettings>
{
    public ClassNotificationAttachedSettingsControl()
    {
        InitializeComponent();
    }

    private void Control_OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (AttachedTimePointTimeType == 1)
        {
            MasterTabControl.SelectedIndex = 2;
        }
    }
}

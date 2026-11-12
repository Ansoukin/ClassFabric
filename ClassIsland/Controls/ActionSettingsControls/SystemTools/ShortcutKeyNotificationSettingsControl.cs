using Avalonia;
using Avalonia.Controls;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Models.Actions.SystemTools;

namespace ClassIsland.Controls.ActionSettingsControls.SystemTools;

/// <summary>
/// 快捷键类 SystemTools 行动的通用设置界面：一个「执行后提醒」开关。
/// 显示切换、锁屏、取消关机计划等一键行动共用。
/// </summary>
public class ShortcutKeyNotificationSettingsControl : ActionSettingsControlBase<ShortcutKeyNotificationSettings>
{
    private CheckBox _notifyCheckBox;

    public ShortcutKeyNotificationSettingsControl()
    {
        var panel = new StackPanel { Spacing = 10, Margin = new(10) };

        _notifyCheckBox = new CheckBox
        {
            Content = "当执行时发出提醒"
        };
        _notifyCheckBox.IsCheckedChanged += (s, e) =>
        {
            Settings.NotifyOnExecute = _notifyCheckBox.IsChecked ?? false;
        };
        panel.Children.Add(_notifyCheckBox);

        Content = panel;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Settings 不能在构造函数里访问（基类约束），回读初值放到挂到视觉树之后。
        _notifyCheckBox.IsChecked = Settings.NotifyOnExecute;
    }
}

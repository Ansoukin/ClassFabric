using Avalonia;
using Avalonia.Controls;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Models.Actions.SystemTools;

namespace ClassIsland.Controls.ActionSettingsControls.SystemTools;

public class ShowFloatingWindowSettingsControl : ActionSettingsControlBase<ShowFloatingWindowSettings>
{
    private readonly ToggleSwitch _toggleSwitch;
    private readonly CheckBox _notifyCheckBox;

    public ShowFloatingWindowSettingsControl()
    {
        var panel = new StackPanel { Spacing = 10, Margin = new(10) };

        _toggleSwitch = new ToggleSwitch
        {
            Content = "显示悬浮窗",
            IsChecked = true
        };
        _toggleSwitch.IsCheckedChanged += (_, _) =>
        {
            Settings.ShowFloatingWindow = _toggleSwitch.IsChecked ?? true;
        };
        panel.Children.Add(_toggleSwitch);

        _notifyCheckBox = new CheckBox { Content = "当执行时发出提醒" };
        _notifyCheckBox.IsCheckedChanged += (_, _) => { Settings.NotifyOnExecute = _notifyCheckBox.IsChecked ?? false; };
        panel.Children.Add(_notifyCheckBox);

        Content = panel;
    }

    // 基类没有插件宿主里的 OnInitialized 钩子，改在挂到可视树时读取设置（此时 Settings 已就绪）。
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _toggleSwitch.IsChecked = Settings.ShowFloatingWindow;
        _notifyCheckBox.IsChecked = Settings.NotifyOnExecute;
    }
}

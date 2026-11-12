using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Models.Actions.SystemTools;

namespace ClassIsland.Controls.ActionSettingsControls.SystemTools;

public class AccentColorSettingsControl : ActionSettingsControlBase<AccentColorSettings>
{
    private readonly ColorPicker _colorPicker;
    private readonly CheckBox _notifyCheckBox;

    public AccentColorSettingsControl()
    {
        var panel = new StackPanel { Spacing = 10, Margin = new(10) };
        panel.Children.Add(new TextBlock
        {
            Text = "切换系统强调色",
            FontSize = 14,
            FontWeight = FontWeight.Bold
        });

        _colorPicker = new ColorPicker
        {
            IsAlphaEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _colorPicker.ColorChanged += (_, _) => Settings.ColorHex = _colorPicker.Color.ToString();

        panel.Children.Add(_colorPicker);

        panel.Children.Add(new TextBlock
        {
            Text = "选择系统的“颜色 / 强调色”。仅切换强调色本身并立即生效，无需重启资源管理器；" +
                   "不会开启“在开始菜单和任务栏上显示强调色”或“在标题栏和窗口边框上显示强调色”，" +
                   "任务栏与窗口边框外观保持你原有的设置。",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray
        });

        _notifyCheckBox = new CheckBox { Content = "当执行时发出提醒" };
        _notifyCheckBox.IsCheckedChanged += (_, _) => Settings.NotifyOnExecute = _notifyCheckBox.IsChecked ?? false;
        panel.Children.Add(_notifyCheckBox);

        Content = panel;
    }

    // 基类没有插件宿主里的 OnInitialized 钩子，改在挂到可视树时读取设置（此时 Settings 已就绪）。
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Color.TryParse(Settings.ColorHex, out var color))
            _colorPicker.Color = color;
        _notifyCheckBox.IsChecked = Settings.NotifyOnExecute;
    }
}

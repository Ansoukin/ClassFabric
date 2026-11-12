using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Models.Automation.Triggers;

namespace ClassIsland.Controls.TriggerSettingsControls;

public class LongIdleTriggerSettingsControl : TriggerSettingsControlBase<LongIdleTriggerSettings>
{
    private readonly NumericUpDown _minutesInput;

    public LongIdleTriggerSettingsControl()
    {
        var panel = new StackPanel { Spacing = 10, Margin = new(10) };

        var inputPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        inputPanel.Children.Add(new TextBlock
        {
            Text = "未操作时长（分钟）:",
            VerticalAlignment = VerticalAlignment.Center
        });

        _minutesInput = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 1440,
            Increment = 1,
            Width = 120
        };
        _minutesInput.ValueChanged += (_, _) => Settings.IdleMinutes = (int)(_minutesInput.Value ?? 1);

        inputPanel.Children.Add(_minutesInput);
        panel.Children.Add(inputPanel);

        panel.Children.Add(new TextBlock
        {
            Text = "当超过设定时间未进行鼠标/键盘操作时触发；\n若启用恢复，则当再次操作电脑时会自动恢复。",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray
        });

        Content = panel;
    }

    // 构造函数里 Settings 还拿不到，挂上可视树后再回读
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _minutesInput.Value = Settings.IdleMinutes;
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Models.Actions.SystemTools;

namespace ClassIsland.Controls.ActionSettingsControls.SystemTools;

/// <summary>
/// 高级计时关机行动的设置界面。
/// </summary>
public class AdvancedShutdownSettingsControl : ActionSettingsControlBase<AdvancedShutdownSettings>
{
    private NumericUpDown _minutesInput;

    public AdvancedShutdownSettingsControl()
    {
        var panel = new StackPanel { Spacing = 10, Margin = new(10) };

        var minutesPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5
        };

        minutesPanel.Children.Add(new TextBlock
        {
            Text = "初始倒计时（分钟）:",
            VerticalAlignment = VerticalAlignment.Center
        });

        _minutesInput = new NumericUpDown
        {
            Width = 120,
            Minimum = 1,
            Maximum = 1440,
            Increment = 1
        };
        _minutesInput.ValueChanged += (_, _) => { Settings.Minutes = (int)(_minutesInput.Value ?? 2); };

        minutesPanel.Children.Add(_minutesInput);
        panel.Children.Add(minutesPanel);

        panel.Children.Add(new TextBlock
        {
            Text = "拥有独立对话框，可已阅、取消计划、延长时间或立即关机。",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray
        });

        Content = panel;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _minutesInput.Value = Settings.Minutes;
    }
}

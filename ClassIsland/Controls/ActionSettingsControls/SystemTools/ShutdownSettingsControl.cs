using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Models.Actions.SystemTools;

namespace ClassIsland.Controls.ActionSettingsControls.SystemTools;

/// <summary>
/// 计时关机行动的设置界面。
/// </summary>
public class ShutdownSettingsControl : ActionSettingsControlBase<ShutdownSettings>
{
    private NumericUpDown _secondsInput;
    private CheckBox _promptCheckBox;

    public ShutdownSettingsControl()
    {
        var panel = new StackPanel { Spacing = 10, Margin = new(10) };

        panel.Children.Add(new TextBlock
        {
            Text = "计时关机设置",
            FontWeight = FontWeight.Bold,
            FontSize = 14
        });

        var secondsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5
        };

        secondsPanel.Children.Add(new TextBlock
        {
            Text = "关机倒计时（秒）:",
            VerticalAlignment = VerticalAlignment.Center
        });

        _secondsInput = new NumericUpDown
        {
            Width = 100,
            Minimum = 0,
            Maximum = 86400,
            Increment = 10
        };

        _secondsInput.ValueChanged += (s, e) => { Settings.Seconds = (int)(_secondsInput.Value ?? 60); };

        secondsPanel.Children.Add(_secondsInput);
        panel.Children.Add(secondsPanel);

        _promptCheckBox = new CheckBox
        {
            Content = "不显示提示",
            Margin = new(0, 5, 0, 0)
        };

        _promptCheckBox.IsCheckedChanged += (s, e) => { Settings.ShowPrompt = !(_promptCheckBox.IsChecked ?? false); };

        panel.Children.Add(_promptCheckBox);

        Content = panel;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _secondsInput.Value = Settings.Seconds;
        _promptCheckBox.IsChecked = !Settings.ShowPrompt;
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Models.Actions.SystemTools;

namespace ClassIsland.Controls.ActionSettingsControls.SystemTools;

public class SetVolumeSettingsControl : ActionSettingsControlBase<SetVolumeSettings>
{
    private readonly NumericUpDown _volumeInput;
    private readonly CheckBox _notifyCheckBox;

    public SetVolumeSettingsControl()
    {
        var panel = new StackPanel { Spacing = 10, Margin = new(10) };

        panel.Children.Add(new TextBlock
        {
            Text = "设置音量百分比",
            Margin = new(0, 0, 0, 5)
        });

        _volumeInput = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 100,
            Increment = 1,
            FormatString = "0",
            PlaceholderText = "输入 0-100 的整数"
        };
        panel.Children.Add(_volumeInput);

        panel.Children.Add(new TextBlock
        {
            Text = "0 = 静音, 100 = 最大音量",
            Foreground = Brushes.Gray,
            FontSize = 12,
            Margin = new(0, 5, 0, 0)
        });

        _notifyCheckBox = new CheckBox { Content = "当执行时发出提醒" };
        _notifyCheckBox.IsCheckedChanged += (_, _) => { Settings.NotifyOnExecute = _notifyCheckBox.IsChecked ?? false; };
        panel.Children.Add(_notifyCheckBox);

        Content = panel;
    }

    // 基类没有插件宿主里的 OnInitialized 钩子，改在挂到可视树时读取设置（此时 Settings 已就绪）。
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _notifyCheckBox.IsChecked = Settings.NotifyOnExecute;

        // 直接绑设置属性，输入框和设置双向同步。
        _volumeInput.Bind(NumericUpDown.ValueProperty, new Binding(nameof(Settings.VolumePercent))
        {
            Source = Settings,
            Mode = BindingMode.TwoWay
        });
    }
}

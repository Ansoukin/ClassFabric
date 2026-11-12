using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Models.Automation.Triggers;

namespace ClassIsland.Controls.TriggerSettingsControls;

public class UsbDeviceTriggerSettingsControl : TriggerSettingsControlBase<UsbDeviceTriggerSettings>
{
    private readonly CheckBox _usbOnlyCheckBox;

    public UsbDeviceTriggerSettingsControl()
    {
        var panel = new StackPanel { Spacing = 10, Margin = new(10) };

        _usbOnlyCheckBox = new CheckBox
        {
            Content = "仅U盘设备",
            IsChecked = true,
            Margin = new(0, 5, 0, 0)
        };
        _usbOnlyCheckBox.IsCheckedChanged += (s, e) =>
        {
            Settings.OnlyUsbStorage = _usbOnlyCheckBox.IsChecked ?? true;
        };
        panel.Children.Add(_usbOnlyCheckBox);

        panel.Children.Add(new TextBlock
        {
            Text = "勾选后仅当U盘设备插入时触发。",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray
        });

        Content = panel;
    }

    // 构造函数里 Settings 还拿不到，挂上可视树后再回读
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _usbOnlyCheckBox.IsChecked = Settings.OnlyUsbStorage;
    }
}

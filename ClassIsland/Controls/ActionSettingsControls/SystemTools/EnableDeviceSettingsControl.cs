using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Models.Actions.SystemTools;

namespace ClassIsland.Controls.ActionSettingsControls.SystemTools;

public class EnableDeviceSettingsControl : ActionSettingsControlBase<EnableDeviceSettings>
{
    private readonly TextBox _deviceIdBox;
    private readonly CheckBox _notifyCheckBox;

    public EnableDeviceSettingsControl()
    {
        var panel = new StackPanel { Spacing = 10, Margin = new(10) };

        panel.Children.Add(new TextBlock
        {
            Text = "设备ID:",
            Margin = new(0, 10, 0, 5)
        });

        _deviceIdBox = new TextBox
        {
            PlaceholderText = "输入设备实例路径"
        };
        panel.Children.Add(_deviceIdBox);

        panel.Children.Add(new TextBlock
        {
            Text = "查询设备ID方法：\n1. 打开设备管理器\n2. 找到目标设备\n3. 右键 → 属性 → 详细信息\n4. 选择\"设备实例路径\"\n5. 复制属性值",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray,
            FontSize = 12,
            Margin = new(0, 10, 0, 0)
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
        _deviceIdBox.Bind(TextBox.TextProperty, new Binding(nameof(Settings.DeviceId))
        {
            Source = Settings
        });
    }
}

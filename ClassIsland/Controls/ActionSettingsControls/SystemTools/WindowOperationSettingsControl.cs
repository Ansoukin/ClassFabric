using Avalonia.Controls;
using Avalonia.Media;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Models.Actions.SystemTools;

namespace ClassIsland.Controls.ActionSettingsControls.SystemTools;

/// <summary>
/// 「窗口操作」行动设置控件。
/// </summary>
public class WindowOperationSettingsControl : ActionSettingsControlBase<WindowOperationSettings>
{
    private CheckBox _notifyCheckBox;
    private ComboBox _operationComboBox;

    public WindowOperationSettingsControl()
    {
        var panel = new StackPanel { Spacing = 10, Margin = new(10) };

        panel.Children.Add(new TextBlock
        {
            Text = "请选择对于活动窗口的操作：",
            FontWeight = FontWeight.Bold,
            FontSize = 14
        });

        // 选项文本与设置模型里的 Operation 键值一致，老用户配置直接命中。
        _operationComboBox = new ComboBox
        {
            Width = 150,
            ItemsSource = new[] { "最大化", "最小化", "向下还原", "关闭窗口" }
        };

        _operationComboBox.SelectionChanged += (s, e) =>
        {
            Settings.Operation = _operationComboBox.SelectedItem?.ToString() ?? "最大化";
        };

        panel.Children.Add(_operationComboBox);
        _notifyCheckBox = new CheckBox { Content = "当执行时发出提醒" };
        _notifyCheckBox.IsCheckedChanged += (s, e) => { Settings.NotifyOnExecute = _notifyCheckBox.IsChecked ?? false; };
        panel.Children.Add(_notifyCheckBox);

        Content = panel;
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _notifyCheckBox.IsChecked = Settings.NotifyOnExecute;
        _operationComboBox.SelectedItem = Settings.Operation;
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Models.Actions.SystemTools;

namespace ClassIsland.Controls.ActionSettingsControls.SystemTools;

public class ThemeSettingsControl : ActionSettingsControlBase<ThemeSettings>
{
    private readonly ComboBox _themeComboBox;
    private readonly CheckBox _notifyCheckBox;

    public ThemeSettingsControl()
    {
        var panel = new StackPanel { Spacing = 10, Margin = new(10) };

        panel.Children.Add(new TextBlock
        {
            Text = "切换主题色",
            FontWeight = FontWeight.Bold,
            FontSize = 14
        });

        var comboPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        comboPanel.Children.Add(new TextBlock
        {
            Text = "选择主题:",
            VerticalAlignment = VerticalAlignment.Center
        });

        _themeComboBox = new ComboBox
        {
            Width = 100,
            ItemsSource = new[] { "浅色", "深色" }
        };

        _themeComboBox.SelectionChanged += (_, _) =>
        {
            Settings.Theme = _themeComboBox.SelectedItem?.ToString() ?? "浅色";
        };

        comboPanel.Children.Add(_themeComboBox);
        panel.Children.Add(comboPanel);

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
        _themeComboBox.SelectedItem = Settings.Theme;
    }
}

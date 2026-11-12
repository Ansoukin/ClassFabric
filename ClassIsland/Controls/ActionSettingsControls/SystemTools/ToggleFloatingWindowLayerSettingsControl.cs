using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Models.Actions.SystemTools;

using Avalonia;
namespace ClassIsland.Controls.ActionSettingsControls.SystemTools;

/// <summary>
/// 切换悬浮窗层级行动的设置控件
/// </summary>
public class ToggleFloatingWindowLayerSettingsControl : ActionSettingsControlBase<ToggleFloatingWindowLayerSettings>
{
    private readonly ComboBox _layerComboBox;
    private readonly CheckBox _notifyCheckBox;

    public ToggleFloatingWindowLayerSettingsControl()
    {
        var panel = new StackPanel { Spacing = 10, Margin = new(10) };

        panel.Children.Add(new TextBlock
        {
            Text = "目标层级:",
            FontWeight = FontWeight.Bold
        });

        _layerComboBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _layerComboBox.Items.Add(new ComboBoxItem { Content = "切换（置底↔置顶）", Tag = -1 });
        _layerComboBox.Items.Add(new ComboBoxItem { Content = "置底", Tag = 0 });
        _layerComboBox.Items.Add(new ComboBoxItem { Content = "置顶", Tag = 1 });
        _layerComboBox.SelectedIndex = 0;
        _layerComboBox.SelectionChanged += OnLayerSelectionChanged;
        panel.Children.Add(_layerComboBox);

        panel.Children.Add(new TextBlock
        {
            Text = "提示：选择\"切换\"会根据当前状态在置顶和置底之间切换，选择具体层级会直接设置到该层级。",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7,
            FontSize = 12
        });

        _notifyCheckBox = new CheckBox { Content = "当执行时发出提醒" };
        _notifyCheckBox.IsCheckedChanged += (_, _) => { Settings.NotifyOnExecute = _notifyCheckBox.IsChecked ?? false; };
        panel.Children.Add(_notifyCheckBox);

        Content = panel;
    }

    private void OnLayerSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_layerComboBox.SelectedItem is ComboBoxItem item && item.Tag is int layer)
        {
            Settings.TargetLayer = layer;
        }
    }

    // 基类没有插件宿主里的 OnInitialized 钩子，改在挂到可视树时读取设置（此时 Settings 已就绪）。
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _notifyCheckBox.IsChecked = Settings.NotifyOnExecute;

        _layerComboBox.SelectedIndex = Settings.TargetLayer switch
        {
            0 => 1,
            1 => 2,
            _ => 0
        };
    }
}

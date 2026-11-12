using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Models.Actions.SystemTools;

using Avalonia;
using ClassIsland.Shared;
namespace ClassIsland.Controls.ActionSettingsControls.SystemTools;

/// <summary>
/// 切换悬浮窗配置方案行动的设置控件
/// </summary>
public class ToggleFloatingWindowProfileSettingsControl : ActionSettingsControlBase<ToggleFloatingWindowProfileSettings>
{
    private readonly ComboBox _profileComboBox;
    private readonly CheckBox _notifyCheckBox;

    public ToggleFloatingWindowProfileSettingsControl()
    {
        var panel = new StackPanel { Spacing = 10, Margin = new(10) };

        panel.Children.Add(new TextBlock
        {
            Text = "目标配置方案:",
            FontWeight = FontWeight.Bold
        });

        _profileComboBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        LoadProfiles();
        _profileComboBox.SelectionChanged += OnProfileSelectionChanged;
        panel.Children.Add(_profileComboBox);

        panel.Children.Add(new TextBlock
        {
            Text = "提示：选择\"切换到下一个\"会按顺序循环切换方案，选择具体方案会直接切换到该方案。",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7,
            FontSize = 12
        });

        _notifyCheckBox = new CheckBox { Content = "当执行时发出提醒" };
        _notifyCheckBox.IsCheckedChanged += (_, _) => { Settings.NotifyOnExecute = _notifyCheckBox.IsChecked ?? false; };
        panel.Children.Add(_notifyCheckBox);

        Content = panel;
    }

    private void LoadProfiles()
    {
        _profileComboBox.Items.Clear();
        _profileComboBox.Items.Add(new ComboBoxItem { Content = "切换到下一个", Tag = null });

        try
        {
            // 控件实例由 DI 创建，服务已就绪；服务异常时至少保住「切换到下一个」这一项
            var profileManager = IAppHost.GetService<ClassIsland.Services.SystemTools.FloatingWindowService>().ProfileManager;
            var profileNames = profileManager.GetProfileNames();

            foreach (var name in profileNames)
            {
                _profileComboBox.Items.Add(new ComboBoxItem
                {
                    Content = name,
                    Tag = name
                });
            }
        }
        catch
        {
            // 服务可能尚未初始化
        }

        _profileComboBox.SelectedIndex = 0;
    }

    private void OnProfileSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_profileComboBox.SelectedItem is ComboBoxItem item)
        {
            Settings.TargetProfileName = item.Tag as string;
        }
    }

    // 基类没有插件宿主里的 OnInitialized 钩子，改在挂到可视树时读取设置（此时 Settings 已就绪）。
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _notifyCheckBox.IsChecked = Settings.NotifyOnExecute;

        var targetName = Settings.TargetProfileName;
        if (string.IsNullOrWhiteSpace(targetName))
        {
            _profileComboBox.SelectedIndex = 0;
            return;
        }

        for (var i = 1; i < _profileComboBox.Items.Count; i++)
        {
            if (_profileComboBox.Items[i] is ComboBoxItem item && item.Tag is string name && name == targetName)
            {
                _profileComboBox.SelectedIndex = i;
                return;
            }
        }

        _profileComboBox.SelectedIndex = 0;
    }
}

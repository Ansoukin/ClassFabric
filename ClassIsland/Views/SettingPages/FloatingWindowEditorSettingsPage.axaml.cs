using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Controls;
using ClassIsland.Core.Controls.Ruleset;
using ClassIsland.Core.Enums.SettingsWindow;
using ClassIsland.Core.Extensions.UI;
using ClassIsland.Core.Models.Ruleset;
using ClassIsland.Services;
using ClassIsland.Services.SystemTools;
using ClassIsland.Shared;
using ClassIsland.ViewModels.SettingsPages;
using FluentAvalonia.UI.Controls;

namespace ClassIsland.Views.SettingPages;

/// <summary>
/// 悬浮窗编辑页。全局外观/层级设置存 SettingsService；按钮布局与按钮/行规则集存悬浮窗配置方案。
/// </summary>
[HidePageTitle]
[SettingsPageInfo("systemtools.floating-window", "悬浮窗编辑",
    ClassIsland.Core.Icons.FluentIcons.LayerDiagonalSparkleRegular,
    ClassIsland.Core.Icons.FluentIcons.LayerDiagonalSparkleRegular,
    SettingsPageCategory.Internal)]
public partial class FloatingWindowEditorSettingsPage : SettingsPageBase
{
    public FloatingWindowEditorSettingsViewModel ViewModel { get; }

    private readonly SettingsService _settingsService;

    private bool _isDisposed;

    private Point? _floatingDragStartPoint;
    private Border? _floatingDragSourceBorder;
    private PointerPressedEventArgs? _floatingDragPressedArgs;
    private static readonly DataFormat<string> FloatingTriggerButtonIdFormat =
        DataFormat.CreateStringApplicationFormat("FloatingTriggerButtonId");

    // ===== 规则集 Drawer 状态 =====
    private enum RulesetTargetType { Button, Row, Window }
    private RulesetTargetType _currentRulesetTarget;
    private FloatingTriggerItem? _currentButtonTarget;
    private FloatingTriggerRow? _currentRowTarget;

    private ToggleSwitch? _drawerIsVisibleToggle;
    private ToggleSwitch? _drawerHideOnRuleToggle;
    private RulesetControl? _drawerRulesetControl;

    private Ruleset? _currentDrawerRuleset;
    private readonly List<INotifyPropertyChanged> _rulesetPropertyListeners = new();

    public FloatingWindowEditorSettingsPage()
    {
        _settingsService = IAppHost.GetService<SettingsService>();
        ViewModel = IAppHost.GetService<FloatingWindowEditorSettingsViewModel>();
        DataContext = this;
        InitializeComponent();

        ViewModel.RefreshFloatingWindowProfiles();
        ViewModel.RefreshFloatingTriggers();
        ViewModel.CurrentFloatingWindowProfile.PropertyChanged += OnProfilePropertyChanged;
        ViewModel.Settings.PropertyChanged += OnSettingsPropertyChanged;
        ViewModel.ProfileChanged += OnViewModelProfileChanged;

        RegisterHidingRulesEvents();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_isDisposed)
        {
            return;
        }

        ViewModel.CurrentFloatingWindowProfile.PropertyChanged -= OnProfilePropertyChanged;
        ViewModel.Settings.PropertyChanged -= OnSettingsPropertyChanged;
        ViewModel.ProfileChanged -= OnViewModelProfileChanged;

        UnregisterHidingRulesEvents();
        DetachRulesetListeners();

        ViewModel.Dispose();
        _isDisposed = true;
    }

    private void OnProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 方案的横排开关直接落盘并刷新窗口，其余布局属性走 PersistFloatingTriggerRows 各自的保存逻辑
        if (e.PropertyName is nameof(ClassIsland.Models.SystemTools.FloatingWindowProfile.FloatingWindowHorizontal))
        {
            IAppHost.GetService<FloatingWindowService>().ProfileManager.SaveProfile();
            IAppHost.GetService<FloatingWindowService>().UpdateWindowState();
        }
    }

    public void ReattachProfilePropertyChanged()
    {
        ViewModel.CurrentFloatingWindowProfile.PropertyChanged -= OnProfilePropertyChanged;
        ViewModel.CurrentFloatingWindowProfile.PropertyChanged += OnProfilePropertyChanged;

        UnregisterHidingRulesEvents();
        RegisterHidingRulesEvents();
    }

    private void RegisterHidingRulesEvents()
    {
        if (ViewModel.Settings.FloatingWindowRuleset is INotifyPropertyChanged hidingRules)
        {
            hidingRules.PropertyChanged += OnHidingRulesPropertyChanged;
        }
    }

    private void UnregisterHidingRulesEvents()
    {
        if (ViewModel.Settings.FloatingWindowRuleset is INotifyPropertyChanged hidingRules)
        {
            hidingRules.PropertyChanged -= OnHidingRulesPropertyChanged;
        }
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 经典材质的外观参数：改完即存盘并让悬浮窗重算窗口状态
        if (e.PropertyName is nameof(ClassIsland.Models.Settings.FloatingWindowTheme)
            or nameof(ClassIsland.Models.Settings.FloatingWindowScale)
            or nameof(ClassIsland.Models.Settings.FloatingWindowIconSize)
            or nameof(ClassIsland.Models.Settings.FloatingWindowTextSize)
            or nameof(ClassIsland.Models.Settings.FloatingWindowOpacity)
            or nameof(ClassIsland.Models.Settings.FloatingWindowShadowEnabled)
            or nameof(ClassIsland.Models.Settings.FloatingWindowDragHandleAlwaysVisible)
            or nameof(ClassIsland.Models.Settings.FloatingWindowLayer)
            or nameof(ClassIsland.Models.Settings.FloatingWindowLayerRecheckMode))
        {
            _settingsService.SaveSettings("悬浮窗编辑页：更新外观/层级设置。");
            IAppHost.GetService<FloatingWindowService>().UpdateWindowState();
        }
        else if (e.PropertyName is nameof(ClassIsland.Models.Settings.ShowFloatingWindow)
            or nameof(ClassIsland.Models.Settings.FloatingWindowRulesetEnabled))
        {
            _settingsService.SaveSettings("悬浮窗编辑页：更新显示开关。");
            IAppHost.GetService<FloatingWindowService>().UpdateWindowState();
            IAppHost.TryGetService<IRulesetService>()?.NotifyStatusChanged();
        }
        else if (e.PropertyName == nameof(ClassIsland.Models.Settings.FloatingWindowRuleset))
        {
            UnregisterHidingRulesEvents();
            RegisterHidingRulesEvents();
            _settingsService.SaveSettings("悬浮窗编辑页：更新整窗隐藏规则集。");
            IAppHost.TryGetService<IRulesetService>()?.NotifyStatusChanged();
        }
    }

    private void OnViewModelProfileChanged(object? sender, EventArgs e)
    {
        ReattachProfilePropertyChanged();
    }

    private void OnHidingRulesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsRulesetStateProperty(e.PropertyName))
        {
            return;
        }

        _settingsService.SaveSettings("悬浮窗编辑页：更新整窗隐藏规则。");
        IAppHost.TryGetService<IRulesetService>()?.NotifyStatusChanged();
    }

    private void OnFloatingWindowVisibleToggleChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch toggle)
        {
            return;
        }

        var service = IAppHost.GetService<FloatingWindowService>();
        var settings = _settingsService.Settings;

        // 没有按钮注册时悬浮窗没有内容，开关直接弹回隐藏
        var shouldShow = toggle.IsChecked == true && service.Entries.Count > 0;
        settings.ShowFloatingWindow = shouldShow;

        if (toggle.IsChecked != shouldShow)
        {
            toggle.IsChecked = shouldShow;
        }

        _settingsService.SaveSettings("悬浮窗编辑页：切换显示开关。");
        service.UpdateWindowState();
    }

    private void OnFloatingWindowProfileSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox || comboBox.SelectedItem is not string profileName)
        {
            return;
        }

        ViewModel.SwitchFloatingWindowProfile(profileName);
    }

    private async void OnAddFloatingWindowProfileClick(object? sender, RoutedEventArgs e)
    {
        var textBox = new TextBox { Text = "" };
        var dialogResult = await new FAContentDialog
        {
            Title = "新建悬浮窗配置方案",
            DefaultButton = FAContentDialogButton.Primary,
            PrimaryButtonText = "创建",
            SecondaryButtonText = "取消",
            Content = new Field
            {
                Content = textBox,
                Label = "配置方案名称",
                Suffix = ".json"
            }
        }.ShowAsyncAuto();

        if (dialogResult != FAContentDialogResult.Primary)
        {
            return;
        }

        var createProfileName = textBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(createProfileName))
        {
            return;
        }

        if (!FloatingWindowProfileManager.IsValidProfileName(createProfileName))
        {
            await new FAContentDialog
            {
                Title = "无法创建配置方案",
                Content = "请使用有效的文件名，避免路径分隔符、末尾的点或空格以及 Windows 保留名称。",
                CloseButtonText = "确定"
            }.ShowAsyncAuto();
            return;
        }

        var path = Path.Combine(ViewModel.FloatingWindowProfilesDirectory,
            createProfileName + ".json");
        if (File.Exists(path))
        {
            return;
        }

        try
        {
            ViewModel.AddFloatingWindowProfile(createProfileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Debug.WriteLine($"[ClassFabric] 创建悬浮窗配置失败: {ex}");
            await new FAContentDialog
            {
                Title = "无法创建配置方案",
                Content = "配置方案未能保存。请检查名称、文件夹写入权限和磁盘空间，然后重试。",
                CloseButtonText = "确定"
            }.ShowAsyncAuto();
        }
    }

    private void OnOpenFloatingWindowProfileFolderClick(object? sender, RoutedEventArgs e)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = Path.GetFullPath(ViewModel.FloatingWindowProfilesDirectory),
            UseShellExecute = true
        });
    }

    private void OnRemoveCurrentProfileClick(object? sender, RoutedEventArgs e)
    {
        var currentName = ViewModel.SelectedFloatingWindowProfile;
        if (string.IsNullOrWhiteSpace(currentName))
        {
            return;
        }

        ViewModel.RemoveFloatingWindowProfile(currentName);
    }

    private void OnAddFloatingTriggerRowClick(object? sender, RoutedEventArgs e)
    {
        ViewModel.AddFloatingTriggerRow();
    }

    private void OnRemoveFloatingTriggerRowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: FloatingTriggerRow row })
        {
            return;
        }

        if (ViewModel.FloatingTriggerRows.Count <= 1)
        {
            return;
        }

        _ = ViewModel.RemoveFloatingTriggerRow(row);
    }

    private void ButtonOpenFloatingWindowRuleset_OnClick(object? sender, RoutedEventArgs e)
    {
        _currentRulesetTarget = RulesetTargetType.Window;
        _currentButtonTarget = null;
        _currentRowTarget = null;

        var settings = _settingsService.Settings;
        OpenRulesetDrawer(settings.FloatingWindowRuleset, true, settings.FloatingWindowRulesetEnabled);
    }

    /// <summary>
    /// 打开规则集 Drawer
    /// </summary>
    private void OpenRulesetDrawer(Ruleset ruleset, bool isVisible, bool hideOnRule)
    {
        DetachRulesetListeners();

        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };

        if (_currentRulesetTarget == RulesetTargetType.Window)
        {
            var hint = new TextBlock
            {
                Text = "此规则集用于控制整窗悬浮窗的隐藏。窗口的“显示 / 隐藏”由设置页顶栏的总开关控制。",
                Foreground = TextFillColorSecondaryBrush(),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 8)
            };
            panel.Children.Add(hint);
        }

        // 开关面板
        var togglesPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 16,
            Margin = new Thickness(22, 0, 0, -15)
        };

        _drawerIsVisibleToggle = new ToggleSwitch
        {
            OnContent = "显示",
            OffContent = "隐藏",
            IsChecked = isVisible,
            IsVisible = _currentRulesetTarget != RulesetTargetType.Window
        };
        ToolTip.SetTip(_drawerIsVisibleToggle, "控制此项目是否显示");
        _drawerIsVisibleToggle.IsCheckedChanged += OnDrawerIsVisibleChanged;

        _drawerHideOnRuleToggle = new ToggleSwitch
        {
            OnContent = "按规则隐藏",
            OffContent = "禁用规则",
            IsChecked = hideOnRule
        };
        ToolTip.SetTip(_drawerHideOnRuleToggle, "启用后，满足规则集条件时自动隐藏");
        _drawerHideOnRuleToggle.IsCheckedChanged += OnDrawerHideOnRuleChanged;

        togglesPanel.Children.Add(_drawerIsVisibleToggle);
        togglesPanel.Children.Add(_drawerHideOnRuleToggle);
        panel.Children.Add(togglesPanel);

        // 规则集编辑器
        _drawerRulesetControl = new RulesetControl { Classes = { "in-drawer" }, Ruleset = ruleset };
        panel.Children.Add(_drawerRulesetControl);

        AttachRulesetListeners(ruleset);

        this.Resources["RulesetDrawerContent"] = panel;
        OpenDrawer("RulesetDrawerContent");
    }

    private IBrush? TextFillColorSecondaryBrush()
    {
        if (Application.Current?.Resources.TryGetResource("TextFillColorSecondaryBrush", null, out var res) == true
            && res is IBrush brush)
        {
            return brush;
        }
        return null;
    }

    private void OnDrawerIsVisibleChanged(object? sender, RoutedEventArgs e)
    {
        var value = _drawerIsVisibleToggle?.IsChecked == true;

        switch (_currentRulesetTarget)
        {
            case RulesetTargetType.Button when _currentButtonTarget != null:
                _currentButtonTarget.Config.IsVisible = value;
                break;
            case RulesetTargetType.Row when _currentRowTarget != null:
                _currentRowTarget.RowRuleset.IsVisible = value;
                break;
        }

        SaveCurrentRulesetTarget();
        IAppHost.GetService<FloatingWindowService>().UpdateWindowState();
        NotifyRulesetStatusChanged();
    }

    private void OnDrawerHideOnRuleChanged(object? sender, RoutedEventArgs e)
    {
        var value = _drawerHideOnRuleToggle?.IsChecked == true;

        switch (_currentRulesetTarget)
        {
            case RulesetTargetType.Button when _currentButtonTarget != null:
                _currentButtonTarget.Config.HideOnRule = value;
                break;
            case RulesetTargetType.Row when _currentRowTarget != null:
                _currentRowTarget.RowRuleset.HideOnRule = value;
                break;
            case RulesetTargetType.Window:
                _settingsService.Settings.FloatingWindowRulesetEnabled = value;
                _settingsService.SaveSettings("悬浮窗编辑页：整窗按规则隐藏开关。");
                break;
        }

        SaveCurrentRulesetTarget();
        IAppHost.GetService<FloatingWindowService>().UpdateWindowState();
        NotifyRulesetStatusChanged();
    }

    private void NotifyRulesetStatusChanged()
    {
        IAppHost.TryGetService<IRulesetService>()?.NotifyStatusChanged();
    }

    private void SaveCurrentRulesetTarget()
    {
        if (_currentRulesetTarget == RulesetTargetType.Window)
        {
            _settingsService.SaveSettings("悬浮窗编辑页：更新规则集目标。");
            return;
        }

        IAppHost.GetService<FloatingWindowService>().ProfileManager.SaveProfile();
    }

    private void AttachRulesetListeners(Ruleset ruleset)
    {
        DetachRulesetListeners();
        _currentDrawerRuleset = ruleset;

        AddRulesetPropertyListener(ruleset);
        ruleset.Groups.CollectionChanged += OnRulesetGroupsCollectionChanged;

        foreach (var group in ruleset.Groups)
        {
            AddRulesetPropertyListener(group);
            group.Rules.CollectionChanged += OnRulesetRulesCollectionChanged;
            foreach (var rule in group.Rules)
            {
                AddRulesetPropertyListener(rule);
            }
        }
    }

    private void DetachRulesetListeners()
    {
        foreach (var listener in _rulesetPropertyListeners)
        {
            listener.PropertyChanged -= OnRulesetPropertyChanged;
        }
        _rulesetPropertyListeners.Clear();

        if (_currentDrawerRuleset != null)
        {
            _currentDrawerRuleset.Groups.CollectionChanged -= OnRulesetGroupsCollectionChanged;
            foreach (var group in _currentDrawerRuleset.Groups)
            {
                group.Rules.CollectionChanged -= OnRulesetRulesCollectionChanged;
            }
            _currentDrawerRuleset = null;
        }
    }

    private void AddRulesetPropertyListener(INotifyPropertyChanged listener)
    {
        listener.PropertyChanged += OnRulesetPropertyChanged;
        _rulesetPropertyListeners.Add(listener);
    }

    private static bool IsRulesetStateProperty(string? propertyName)
    {
        return propertyName == nameof(Ruleset.State)
            || propertyName == nameof(RuleGroup.State)
            || propertyName == nameof(Rule.State);
    }

    private void OnRulesetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsRulesetStateProperty(e.PropertyName))
        {
            return;
        }

        SaveCurrentRulesetTarget();
        NotifyRulesetStatusChanged();
        IAppHost.TryGetService<FloatingWindowService>()?.UpdateWindowState();
    }

    private void OnRulesetGroupsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_currentDrawerRuleset == null)
        {
            return;
        }

        var ruleset = _currentDrawerRuleset;
        DetachRulesetListeners();
        AttachRulesetListeners(ruleset);

        SaveCurrentRulesetTarget();
        NotifyRulesetStatusChanged();
        IAppHost.TryGetService<FloatingWindowService>()?.UpdateWindowState();
    }

    private void OnRulesetRulesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_currentDrawerRuleset == null)
        {
            return;
        }

        var ruleset = _currentDrawerRuleset;
        DetachRulesetListeners();
        AttachRulesetListeners(ruleset);

        SaveCurrentRulesetTarget();
        NotifyRulesetStatusChanged();
        IAppHost.TryGetService<FloatingWindowService>()?.UpdateWindowState();
    }

    // ===== 选中状态与拖拽 =====

    private void OnFloatingTriggerItemSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: FloatingTriggerItem item })
        {
            return;
        }

        _currentRulesetTarget = RulesetTargetType.Button;
        _currentButtonTarget = item;
        _currentRowTarget = null;

        OpenRulesetDrawer(item.Config.HidingRules, item.Config.IsVisible, item.Config.HideOnRule);
    }

    private void OnRowRulesetClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: FloatingTriggerRow row })
        {
            return;
        }

        _currentRulesetTarget = RulesetTargetType.Row;
        _currentButtonTarget = null;
        _currentRowTarget = row;

        OpenRulesetDrawer(row.RowRuleset.HidingRules, row.RowRuleset.IsVisible, row.RowRuleset.HideOnRule);
    }

    private void OnInsertRowBelowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: FloatingTriggerRow row })
        {
            return;
        }

        var index = ViewModel.FloatingTriggerRows.IndexOf(row);
        if (index < 0)
        {
            return;
        }

        ViewModel.InsertFloatingTriggerRow(index + 1);
    }

    private void OnFloatingTriggerItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border || !e.GetCurrentPoint(border).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _floatingDragSourceBorder = border;
        _floatingDragStartPoint = e.GetPosition(border);
        _floatingDragPressedArgs = e;
        e.Pointer.Capture(border);
        e.Handled = e.Pointer.Type is PointerType.Touch or PointerType.Pen;
    }

    private void OnFloatingTriggerItemPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer.Captured == sender)
        {
            e.Pointer.Capture(null);
        }
        _floatingDragSourceBorder = null;
        _floatingDragStartPoint = null;
        _floatingDragPressedArgs = null;
    }

    private async void OnFloatingTriggerItemPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Border border || _floatingDragSourceBorder != border || _floatingDragStartPoint == null)
        {
            return;
        }

        if (!e.GetCurrentPoint(border).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var now = e.GetPosition(border);
        if (Math.Abs(now.X - _floatingDragStartPoint.Value.X) + Math.Abs(now.Y - _floatingDragStartPoint.Value.Y) < 4)
        {
            return;
        }

        if (border.Tag is not string buttonId || string.IsNullOrWhiteSpace(buttonId))
        {
            return;
        }

        if (_floatingDragPressedArgs == null)
        {
            return;
        }

        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(FloatingTriggerButtonIdFormat, buttonId));

        _floatingDragSourceBorder = null;
        _floatingDragStartPoint = null;
        var isTouchOrPen = e.Pointer.Type is PointerType.Touch or PointerType.Pen;
        await DragDrop.DoDragDropAsync(_floatingDragPressedArgs, data, DragDropEffects.Move);
        _floatingDragPressedArgs = null;
        e.Handled = isTouchOrPen;
    }

    private static bool TryGetDragButtonId(DragEventArgs e, out string buttonId)
    {
        buttonId = e.DataTransfer.TryGetValue(FloatingTriggerButtonIdFormat) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(buttonId);
    }

    private int GetRowIndexFromControl(Control? control)
    {
        var current = control;
        while (current != null)
        {
            if (current.DataContext is FloatingTriggerRow row)
            {
                return ViewModel.FloatingTriggerRows.IndexOf(row);
            }

            current = current.GetVisualParent() as Control;
        }

        return -1;
    }

    private int GetRowInsertIndex(Control sender, FloatingTriggerRow row, DragEventArgs e)
    {
        if (row.Buttons.Count == 0)
        {
            return 0;
        }

        var pointer = e.GetPosition(sender);
        var itemsControl = sender as ItemsControl
                           ?? sender.GetVisualDescendants()
                               .OfType<ItemsControl>()
                               .FirstOrDefault(x => ReferenceEquals(x.ItemsSource, row.Buttons));
        if (itemsControl == null)
        {
            return row.Buttons.Count;
        }

        for (var i = 0; i < row.Buttons.Count; i++)
        {
            var container = itemsControl.ContainerFromIndex(i);
            var topLeft = container?.TranslatePoint(new Point(0, 0), sender);
            if (topLeft == null)
            {
                continue;
            }

            var center = topLeft.Value.X + container!.Bounds.Width / 2;
            if (pointer.X <= center)
            {
                return i;
            }
        }

        return row.Buttons.Count;
    }

    private void OnFloatingTriggerRowDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = TryGetDragButtonId(e, out _) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnFloatingTriggerRowDrop(object? sender, DragEventArgs e)
    {
        if (!TryGetDragButtonId(e, out var buttonId) || sender is not Control senderControl)
        {
            return;
        }

        var rowIndex = GetRowIndexFromControl(senderControl);
        if (rowIndex < 0)
        {
            return;
        }

        var row = ViewModel.FloatingTriggerRows[rowIndex];
        var insertIndex = GetRowInsertIndex(senderControl, row, e);
        ViewModel.MoveFloatingTrigger(buttonId, rowIndex, insertIndex);
        e.Handled = true;
    }

    private void OnFloatingTriggerItemDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = TryGetDragButtonId(e, out _) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnFloatingTriggerItemDrop(object? sender, DragEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not FloatingTriggerItem targetItem)
        {
            return;
        }

        if (!TryGetDragButtonId(e, out var buttonId))
        {
            return;
        }

        var rowIndex = GetRowIndexFromControl(border);
        if (rowIndex < 0)
        {
            return;
        }

        var row = ViewModel.FloatingTriggerRows[rowIndex];
        var targetIndex = row.Buttons.IndexOf(targetItem);
        if (targetIndex < 0)
        {
            return;
        }

        var pos = e.GetPosition(border);
        if (pos.X > border.Bounds.Width / 2)
        {
            targetIndex += 1;
        }

        ViewModel.MoveFloatingTrigger(buttonId, rowIndex, targetIndex);
        e.Handled = true;
    }
}

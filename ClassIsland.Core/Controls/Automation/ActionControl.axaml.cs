using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Models.Automation;
using ClassIsland.Shared;
using ClassIsland.Shared.Models.Automation;
using CommunityToolkit.Mvvm.Input;
namespace ClassIsland.Core.Controls.Automation;

/// <summary>
/// 用于显示和编辑 <see cref="ActionSet"/>（行动组）的控件。
/// </summary>
/// <seealso cref="ActionItemControl"/>
public partial class ActionControl : UserControl
{
    /// <inheritdoc cref="ActionControl" />
    public ActionControl()
    {
        InitializeComponent();
        AddActionButton.Click += AddActionButton_OnClick;
    }

    IActionService ActionService { get; } = IAppHost.GetService<IActionService>();

    [RelayCommand]
    void AddAction(ActionMenuTreeItem menu)
    {
        ActionSet.ActionItems.Add(AutomationPickerDelivery.CreateActionItem(menu));
    }

    /// <inheritdoc cref="ActionSet"/>
    public static readonly StyledProperty<ActionSet> ActionSetProperty =
        AvaloniaProperty.Register<ActionControl, ActionSet>(nameof(ActionSet));

    /// <summary>
    /// 要显示的行动组。
    /// </summary>
    public ActionSet ActionSet
    {
        get => GetValue(ActionSetProperty);
        set => SetValue(ActionSetProperty, value);
    }

    /// <summary>
    /// 是否用选择面板替代旧级联菜单。默认关闭，档案时间点行动、教程编辑器等
    /// 未挂载面板的宿主保持旧行为；自动化设置页置 true 启用面板。
    /// </summary>
    public static readonly StyledProperty<bool> UsePanelPickerProperty =
        AvaloniaProperty.Register<ActionControl, bool>(nameof(UsePanelPicker));

    public bool UsePanelPicker
    {
        get => GetValue(UsePanelPickerProperty);
        set => SetValue(UsePanelPickerProperty, value);
    }

    FlyoutBase? _addFlyout;

    void UpdateFlyout()
    {
        // 面板模式下摘掉旧级联菜单，避免点击按钮时菜单仍自动弹出。
        _addFlyout ??= AddActionButton.Flyout;
        AddActionButton.Flyout = UsePanelPicker ? null : _addFlyout;
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateFlyout();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == UsePanelPickerProperty)
        {
            UpdateFlyout();
        }
    }

    void AddActionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!UsePanelPicker)
        {
            // 旧级联菜单由 Flyout 自动弹出，这里不处理。
            return;
        }
        // 非定向请求：落地走宿主页面投递，面板内可切到触发器模式。
        this.ExecutePick(AutomationPickerKind.Action);
    }

    static ActionControl()
    {
        ActionSetProperty.Changed.AddClassHandler<ActionControl>((sender, e) =>
        {
            var newValue = (ActionSet)e.NewValue;
            sender.ActionService.MigrateActionSet(newValue);
        });
    }

}

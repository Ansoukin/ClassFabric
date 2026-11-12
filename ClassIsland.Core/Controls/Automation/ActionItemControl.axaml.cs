using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Helpers.UI;
using ClassIsland.Core.Models.Automation;
using ClassIsland.Core.Models.UI;
using ClassIsland.Shared;
using ClassIsland.Shared.Models.Automation;
using CommunityToolkit.Mvvm.Input;
namespace ClassIsland.Core.Controls.Automation;

/// <summary>
/// 用于显示和编辑 <see cref="ActionItem"/>（行动项）的控件。
/// </summary>
/// <seealso cref="ActionControl"/>
public partial class ActionItemControl : UserControl
{
    public ActionItemControl()
    {
        InitializeComponent();
        ChangeActionButton.Click += ChangeActionButton_OnClick;
    }

    public IActionService ActionService { get; } = IAppHost.GetService<IActionService>();

    /// <summary>
    /// 是否用选择面板替代旧级联菜单；跟随宿主 <see cref="ActionControl"/> 的设置。
    /// </summary>
    bool UsePanelPicker => this.FindAncestorOfType<ActionControl>()?.UsePanelPicker ?? false;

    FlyoutBase? _changeFlyout;

    void UpdateFlyout()
    {
        // 面板模式下摘掉旧级联菜单，避免点击按钮时菜单仍自动弹出。
        _changeFlyout ??= ChangeActionButton.Flyout;
        ChangeActionButton.Flyout = UsePanelPicker ? null : _changeFlyout;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateFlyout();
        ActionService.MigrateActionItem(ActionItem);
        UpdateContent();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Unload();
    }

    void UpdateContent()
    {
        var newControl = ActionSettingsControlBase.GetInstance(ActionItem);

        ActionInfoIconText.IconSource =
            IActionService.ActionInfos.TryGetValue(ActionItem.Id, out var actionInfo)
                ? actionInfo.IconSource
                : IconExpressionHelper.TryParseOrNull("\uee31");
        ActionInfoIconText.Text = actionInfo?.Name ?? $"{ActionItem.Id}（未知行动）";

        if (newControl != null)
        {
            newControl.ActionNameChanged += ControlOnActionNameChanged;
            newControl.ActionIconChanged += ControlOnActionIconChanged;
            RootContentPresenter.Content = newControl;
            RootContentPresenter.IsVisible = true;
            newControl.IsNewAdded = ActionItem.IsNewAdded;
        }
        else
        {
            RootContentPresenter.Content = null;
            RootContentPresenter.IsVisible = false;
        }
        ActionItem.IsNewAdded = false;
    }

    void ControlOnActionNameChanged(object? sender, string e) => ActionInfoIconText.Text = e;
    void ControlOnActionIconChanged(object? sender, FluentAvalonia.UI.Controls.FAIconSource? e) =>
        ActionInfoIconText.IconSource = e;


    void Unload()
    {
        if (RootContentPresenter.Content is ActionSettingsControlBase oldControl)
        {
            oldControl.ActionNameChanged -= ControlOnActionNameChanged;
            oldControl.ActionIconChanged -= ControlOnActionIconChanged;
        }

        RootContentPresenter.Content = null;
    }



    [RelayCommand]
    void ChangeAction((object T1, object T2) p)
    {
        if (p is { T1: ActionItem actionItem, T2: ActionMenuTreeItem menu })
        {
            ChangeAction(actionItem, menu);
        }
    }

    void ChangeAction(ActionItem actionItem, ActionMenuTreeItem menu)
    {
        actionItem.Id = menu.ActionItemId;

        if (menu.GetType().IsGenericType &&
            menu.GetType().GetGenericTypeDefinition() == typeof(ActionMenuTreeItem<>))
        {
            var settingsType = menu.GetType().GetGenericArguments()[0];
            var settings = actionItem.Settings;
            if (settings?.GetType() != settingsType)
            {
                Unload();
                ActionItem.IsNewAdded = true;
                settings = Activator.CreateInstance(settingsType);
            }

            var setterProperty = menu.GetType()
                .GetProperty(nameof(ActionMenuTreeItem<object>.ActionItemSettingsSetter));
            var setter = setterProperty?.GetValue(menu) as Delegate;
            setter?.DynamicInvoke(settings);
            actionItem.Settings = settings;
        }
        else
        {
            Unload();
            ActionItem.IsNewAdded = true;
        }

        UpdateContent();
    }

    void ChangeActionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!UsePanelPicker)
        {
            // 旧级联菜单由 Flyout 自动弹出，这里不处理。
            return;
        }
        // 换类型是定向场景：锁定行动模式，选中后回写该行动项。
        var actionItem = ActionItem;
        this.ExecutePick(AutomationPickerKind.Action, request =>
        {
            request.LockMode = true;
            request.Callback = item =>
            {
                if (item.MenuSource != null)
                {
                    ChangeAction(actionItem, item.MenuSource);
                }
            };
            request.ExistingIdsProvider = () => ActionSet?.ActionItems.Select(a => a.Id) ?? Enumerable.Empty<string>();
        });
    }

    [RelayCommand]
    void RemoveAction(ActionItem actionItem)
    {
        if (actionItem.IsWorking) return;

        if (RootContentPresenter.Content is ActionSettingsControlBase controlBase)
        {
            if (controlBase.ShouldShowUndoDeleteButton())
            {
                var index = ActionSet.ActionItems.IndexOf(actionItem);

                var revertButton = new Button { Content = "撤销" };
                var toastMessage = new ToastMessage($"已删除行动“{ActionInfoIconText.Text}”。")
                {
                    ActionContent = revertButton,
                    Duration = TimeSpan.FromSeconds(10)
                };
                revertButton.Click += (o, args) =>
                {
                    ActionSet.ActionItems.Insert(Math.Min(index, ActionSet.ActionItems.Count), actionItem);
                    toastMessage.Close();
                };
                this.ShowToast(toastMessage);
            }
        }

        ActionSet.ActionItems.Remove(actionItem);
    }




    public static readonly StyledProperty<ActionItem> ActionItemProperty =
        AvaloniaProperty.Register<ActionItemControl, ActionItem>(nameof(ActionItem));

    public ActionItem ActionItem
    {
        get => GetValue(ActionItemProperty);
        set => SetValue(ActionItemProperty, value);
    }

    ActionSet? _actionSet;
    public ActionSet ActionSet => _actionSet ??= this.FindAncestorOfType<ActionControl>()?.GetValue(ActionControl.ActionSetProperty);
}

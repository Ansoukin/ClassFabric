using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ClassIsland.Core.Models.Automation;
using CommunityToolkit.Mvvm.Input;
namespace ClassIsland.Core.Controls.Automation;

/// <summary>
/// TriggerSettingsControl.xaml 的交互逻辑
/// </summary>
public partial class TriggerSettingsControl : UserControl
{
    public static readonly StyledProperty<ObservableCollection<TriggerSettings>> TriggersProperty = AvaloniaProperty.Register<TriggerSettingsControl, ObservableCollection<TriggerSettings>>(
        nameof(Triggers));

    public ObservableCollection<TriggerSettings> Triggers
    {
        get => GetValue(TriggersProperty);
        set => SetValue(TriggersProperty, value);
    }

    /// <summary>
    /// 是否用选择面板替代行内下拉。默认关闭，自动化设置页置 true 启用面板。
    /// </summary>
    public static readonly StyledProperty<bool> UsePanelPickerProperty =
        AvaloniaProperty.Register<TriggerSettingsControl, bool>(nameof(UsePanelPicker));

    public bool UsePanelPicker
    {
        get => GetValue(UsePanelPickerProperty);
        set => SetValue(UsePanelPickerProperty, value);
    }

    public TriggerSettingsControl()
    {
        InitializeComponent();
    }

    private void ButtonAddTrigger_OnClick(object sender, RoutedEventArgs e)
    {
        if (UsePanelPicker)
        {
            // 面板模式：落地由宿主页面按面板当前模式投递，面板内可切到行动模式再选。
            this.ExecutePick(AutomationPickerKind.Trigger);
        }
        else
        {
            // 经典模式：先加空行，再由行内下拉选择类型。
            Triggers.Add(new TriggerSettings());
        }
    }

    [RelayCommand]
    public void PickForTrigger(TriggerSettings trigger)
    {
        // 行内换类型是定向场景：锁定触发器模式，选中后回写该行 ID。
        this.ExecutePick(AutomationPickerKind.Trigger, request =>
        {
            request.LockMode = true;
            request.Callback = item => trigger.Id = item.Id;
            request.ExistingIdsProvider = () => Triggers.Select(t => t.Id);
        });
    }

    [RelayCommand]
    public void RemoveTrigger(TriggerSettings trigger)
    {
        Triggers.Remove(trigger);
    }
}

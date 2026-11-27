using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Services;
using ClassIsland.Views.SettingPages;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Workflow = ClassIsland.Core.Models.Automation.Workflow;

namespace ClassIsland.ViewModels.SettingsPages;

public partial class AutomationSettingsViewModel : ObservableRecipient
{
    public IRulesetService RulesetService { get; }
    public SettingsService SettingsService { get; }
    public IAutomationService AutomationService { get; }
    public IActionService ActionService { get; }
    public ILogger<AutomationSettingsPage> Logger { get; }

    public AutomationSettingsViewModel(
        IRulesetService rulesetService,
        SettingsService settingsService,
        ILogger<AutomationSettingsPage> logger,
        IAutomationService automationService,
        IActionService actionService)
    {
        RulesetService = rulesetService;
        SettingsService = settingsService;
        Logger = logger;
        AutomationService = automationService;
        ActionService = actionService;
    }

    [ObservableProperty]
    bool _isPanelOpened = false;

    partial void OnIsPanelOpenedChanged(bool value)
    {
        if (!value)
            SelectedWorkflow = null;
    }

    [ObservableProperty]
    Workflow? _selectedWorkflow;

    /// <summary>
    /// 自动化编辑器的触发器/行动选择是否使用新面板；关闭则回退行内下拉与级联菜单。
    /// 持久化在应用设置里，供选择面板的“切换为经典菜单”入口改写。
    /// </summary>
    public bool IsPanelPickerEnabled
    {
        get => SettingsService.Settings.IsAutomationPanelPickerEnabled;
        set
        {
            SettingsService.Settings.IsAutomationPanelPickerEnabled = value;
            OnPropertyChanged();
            // 选择面板的“切换为经典菜单”入口会改写本属性，下拉框需同步选中项。
            OnPropertyChanged(nameof(MenuSelectionModeIndex));
        }
    }

    /// <summary>
    /// 菜单选择方式的下拉索引：0 = 全屏选择面板，1 = 下拉与级联菜单。
    /// 只是 <see cref="IsPanelPickerEnabled"/> 的下拉包装，持久化仍是应用设置里的布尔值。
    /// </summary>
    public int MenuSelectionModeIndex
    {
        get => IsPanelPickerEnabled ? 0 : 1;
        set => IsPanelPickerEnabled = value == 0;
    }
}

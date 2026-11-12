using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Models.SystemTools;
using ClassIsland.Services.SystemTools;
using ClassIsland.Shared;
using CommunityToolkit.Mvvm.ComponentModel;

using ClassIsland.Services;
namespace ClassIsland.ViewModels.SettingsPages;

/// <summary>
/// 悬浮窗编辑页里的一颗按钮。图标源按表达式现解析，供 Chip 上的 FAIconSourceElement 显示。
/// </summary>
public partial class FloatingTriggerItem : ObservableObject
{
    [ObservableProperty] private string _buttonId = string.Empty;
    [ObservableProperty] private string _icon = string.Empty;
    [ObservableProperty] private string _buttonName = string.Empty;
    [ObservableProperty] private bool _isRulesetExpanded = false;
    [ObservableProperty] private ButtonRulesetConfig _config = new();

    /// <summary>
    /// 图标表达式解析出的图标源，供 IconSourceElement 显示。兼容旧版 /uXXXX 配置。
    /// </summary>
    public FluentAvalonia.UI.Controls.FAIconSource? IconSource =>
        FloatingWindowIconProvider.TryResolveIconSource(Icon);

    partial void OnIconChanged(string value) { OnPropertyChanged(nameof(IconSource)); }
}

/// <summary>
/// 悬浮窗编辑页里的一个按钮行。
/// </summary>
public partial class FloatingTriggerRow : ObservableObject
{
    [ObservableProperty] private ObservableCollection<FloatingTriggerItem> _buttons = new();
    [ObservableProperty] private int _rowIndex = 0;
    [ObservableProperty] private RowRulesetConfig _rowRuleset = new();
    [ObservableProperty] private bool _isRulesetExpanded = false;
}

/// <summary>
/// 悬浮窗编辑页的视图模型。全局外观/层级设置直接读写 SettingsService.Settings；
/// 按钮布局与按钮/行规则集存悬浮窗配置方案，经 FloatingWindowService.ProfileManager 读写。
/// 从插件版 SystemToolsSettingsViewModel 抽出的悬浮窗切片。
/// </summary>
public partial class FloatingWindowEditorSettingsViewModel : ObservableObject, IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly FloatingWindowService _floatingWindowService;
    private readonly EventHandler _entriesChangedHandler;

    [ObservableProperty] private ObservableCollection<FloatingTriggerRow> _floatingTriggerRows = new();
    [ObservableProperty] private bool _hasFloatingTriggerEntries;

    // 选中状态
    [ObservableProperty] private FloatingTriggerRow? _selectedFloatingTriggerRow;
    [ObservableProperty] private FloatingTriggerItem? _selectedFloatingTriggerItem;

    // 悬浮窗配置方案
    [ObservableProperty] private ObservableCollection<string> _floatingWindowProfileNames = new();
    [ObservableProperty] private string _selectedFloatingWindowProfile = "Default";

    public FloatingWindowEditorSettingsViewModel(SettingsService settingsService,
        FloatingWindowService floatingWindowService)
    {
        _settingsService = settingsService;
        _floatingWindowService = floatingWindowService;
        // 服务里的按钮注册集变了（触发器 Loaded/UnLoaded、改名、换图标）就整页重算，省得逐项对账
        _entriesChangedHandler = (_, _) => Dispatcher.UIThread.Post(RefreshFloatingTriggers);
        _floatingWindowService.EntriesChanged += _entriesChangedHandler;
    }

    /// <summary>全局设置（外观/层级/整窗规则集等），悬浮窗编辑页直接绑定。</summary>
    public ClassIsland.Models.Settings Settings => _settingsService.Settings;

    public FloatingWindowProfile CurrentFloatingWindowProfile => _floatingWindowService.ProfileManager.CurrentProfile;

    /// <summary>
    /// 悬浮窗方案 JSON 文件所在目录，供 UI 层打开文件夹/重名检测使用。
    /// </summary>
    public string FloatingWindowProfilesDirectory => _floatingWindowService.ProfileManager.ProfilesDirectory;

    public void RefreshFloatingWindowProfiles()
    {
        var names = _floatingWindowService.ProfileManager.GetProfileNames();
        FloatingWindowProfileNames.Clear();
        foreach (var name in names)
        {
            FloatingWindowProfileNames.Add(name);
        }
        SelectedFloatingWindowProfile = _floatingWindowService.ProfileManager.CurrentProfileName;
    }

    public void RefreshFloatingTriggers()
    {
        _floatingWindowService.EnsureUniqueButtonIds();
        var entries = _floatingWindowService.Entries
            .GroupBy(x => x.ButtonId)
            .ToDictionary(x => x.Key, x => x.First());
        HasFloatingTriggerEntries = entries.Count > 0;

        var settings = _settingsService.Settings;
        var profile = CurrentFloatingWindowProfile;
        var globalShow = settings.ShowFloatingWindow;
        if (!HasFloatingTriggerEntries && globalShow)
        {
            // 没有按钮时显示开关没有意义，顺手关掉并同步给悬浮窗
            settings.ShowFloatingWindow = false;
            _settingsService.SaveSettings("悬浮窗编辑页：无按钮时收起显示开关。");
            _floatingWindowService.UpdateWindowState();
        }

        // 清理不存在的按钮ID
        if (profile.PruneInvalidButtonIds(entries.Keys))
        {
            _floatingWindowService.ProfileManager.SaveProfile();
        }

        // 收集已配置的按钮ID
        var configuredIds = new HashSet<string>();
        foreach (var row in profile.FloatingWindowButtonRows ?? [])
        {
            foreach (var id in row)
            {
                configuredIds.Add(id);
            }
        }

        // 如果没有任何按钮被配置到行中，自动将所有可用按钮添加到第一行
        // 这样用户首次使用或从插件版迁移时，按钮默认会显示出来
        if (configuredIds.Count == 0 && entries.Count > 0)
        {
            var allButtonIds = entries.Values.Select(e => e.ButtonId).ToList();
            if (profile.FloatingWindowButtonRows == null || profile.FloatingWindowButtonRows.Count == 0)
            {
                profile.FloatingWindowButtonRows = [allButtonIds];
            }
            else
            {
                profile.FloatingWindowButtonRows[0] = allButtonIds;
            }
            foreach (var id in allButtonIds)
            {
                configuredIds.Add(id);
            }
            _floatingWindowService.ProfileManager.SaveProfile();
        }

        // 新注册且尚未配置的按钮自动追加到第一行
        // 已存在按钮配置（如被用户移除/隐藏）的按钮不再自动添加
        var newButtonIds = entries.Values
            .Where(e => !configuredIds.Contains(e.ButtonId))
            .Where(e => !profile.FloatingWindowButtonRulesets.ContainsKey(e.ButtonId))
            .Select(e => e.ButtonId)
            .ToList();
        if (newButtonIds.Count > 0)
        {
            if (profile.FloatingWindowButtonRows == null || profile.FloatingWindowButtonRows.Count == 0)
            {
                profile.FloatingWindowButtonRows = [newButtonIds];
            }
            else
            {
                profile.FloatingWindowButtonRows[0] = [.. profile.FloatingWindowButtonRows[0], .. newButtonIds];
            }
            foreach (var id in newButtonIds)
            {
                configuredIds.Add(id);
            }
            _floatingWindowService.ProfileManager.SaveProfile();
        }

        // 注销旧对象上的事件处理程序，避免重复注册和内存泄漏
        foreach (var oldRow in FloatingTriggerRows)
        {
            oldRow.RowRuleset.PropertyChanged -= OnRowRulesetPropertyChanged;
            if (oldRow.RowRuleset.HidingRules is INotifyPropertyChanged oldRowHidingRules)
            {
                oldRowHidingRules.PropertyChanged -= OnRowRulesetPropertyChanged;
            }
            foreach (var oldItem in oldRow.Buttons)
            {
                oldItem.Config.PropertyChanged -= OnButtonConfigPropertyChanged;
                if (oldItem.Config.HidingRules is INotifyPropertyChanged oldBtnHidingRules)
                {
                    oldBtnHidingRules.PropertyChanged -= OnButtonConfigPropertyChanged;
                }
            }
        }

        // 构建已配置的行显示
        FloatingTriggerRows.Clear();
        var rowConfigs = profile.FloatingWindowRowRulesets;
        var rowIndex = 0;
        var needSave = false;
        foreach (var row in profile.FloatingWindowButtonRows ?? [])
        {
            while (rowConfigs.Count <= rowIndex)
            {
                rowConfigs.Add(new RowRulesetConfig());
                needSave = true;
            }
            var vmRow = new FloatingTriggerRow
            {
                RowIndex = rowIndex + 1,
                RowRuleset = rowConfigs[rowIndex]
            };
            vmRow.RowRuleset.PropertyChanged += OnRowRulesetPropertyChanged;
            if (vmRow.RowRuleset.HidingRules is INotifyPropertyChanged rowHidingRules)
            {
                rowHidingRules.PropertyChanged += OnRowRulesetPropertyChanged;
            }
            foreach (var id in row)
            {
                if (!entries.TryGetValue(id, out var entry))
                {
                    continue;
                }
                if (!profile.FloatingWindowButtonRulesets.TryGetValue(entry.ButtonId, out var btnConfig))
                {
                    btnConfig = new ButtonRulesetConfig();
                    profile.FloatingWindowButtonRulesets[entry.ButtonId] = btnConfig;
                    needSave = true;
                }
                var item = new FloatingTriggerItem
                {
                    ButtonId = entry.ButtonId,
                    Icon = entry.Icon,
                    ButtonName = entry.LayoutName,
                    Config = btnConfig
                };
                item.Config.PropertyChanged += OnButtonConfigPropertyChanged;
                if (item.Config.HidingRules is INotifyPropertyChanged btnHidingRules)
                {
                    btnHidingRules.PropertyChanged += OnButtonConfigPropertyChanged;
                }
                vmRow.Buttons.Add(item);
            }
            FloatingTriggerRows.Add(vmRow);
            rowIndex++;
        }

        if (FloatingTriggerRows.Count == 0)
        {
            if (rowConfigs.Count == 0)
            {
                rowConfigs.Add(new RowRulesetConfig());
                needSave = true;
            }
            var emptyRow = new FloatingTriggerRow
            {
                RowIndex = 1,
                RowRuleset = rowConfigs[0]
            };
            emptyRow.RowRuleset.PropertyChanged += OnRowRulesetPropertyChanged;
            if (emptyRow.RowRuleset.HidingRules is INotifyPropertyChanged emptyRowHidingRules)
            {
                emptyRowHidingRules.PropertyChanged += OnRowRulesetPropertyChanged;
            }
            FloatingTriggerRows.Add(emptyRow);
        }

        // 如果有新创建的默认配置，确保保存
        if (needSave)
        {
            _floatingWindowService.ProfileManager.SaveProfile();
        }
    }

    private void OnButtonConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 规则集求值时会写入 State（Ruleset/RuleGroup/Rule），避免因此递归触发通知
        if (IsRulesetStateProperty(e.PropertyName))
        {
            return;
        }

        _floatingWindowService.ProfileManager.SaveProfile();
        _floatingWindowService.UpdateWindowState();
        IAppHost.TryGetService<IRulesetService>()?.NotifyStatusChanged();
    }

    private void OnRowRulesetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 规则集求值时会写入 State（Ruleset/RuleGroup/Rule），避免因此递归触发通知
        if (IsRulesetStateProperty(e.PropertyName))
        {
            return;
        }

        _floatingWindowService.ProfileManager.SaveProfile();
        _floatingWindowService.UpdateWindowState();
        IAppHost.TryGetService<IRulesetService>()?.NotifyStatusChanged();
    }

    private static bool IsRulesetStateProperty(string? propertyName)
    {
        return propertyName == nameof(ClassIsland.Core.Models.Ruleset.Ruleset.State)
            || propertyName == nameof(ClassIsland.Core.Models.Ruleset.RuleGroup.State)
            || propertyName == nameof(ClassIsland.Core.Models.Ruleset.Rule.State);
    }

    public void AddFloatingTriggerRow()
    {
        var profile = CurrentFloatingWindowProfile;
        var rowRulesets = profile.FloatingWindowRowRulesets;
        var newRowRuleset = new RowRulesetConfig();
        rowRulesets.Add(newRowRuleset);
        var newRow = new FloatingTriggerRow
        {
            RowIndex = FloatingTriggerRows.Count + 1,
            RowRuleset = newRowRuleset
        };
        newRow.RowRuleset.PropertyChanged += OnRowRulesetPropertyChanged;
        if (newRow.RowRuleset.HidingRules is INotifyPropertyChanged rowHidingRules)
        {
            rowHidingRules.PropertyChanged += OnRowRulesetPropertyChanged;
        }
        FloatingTriggerRows.Add(newRow);
        PersistFloatingTriggerRows();
    }

    public void InsertFloatingTriggerRow(int insertIndex)
    {
        var profile = CurrentFloatingWindowProfile;
        var rowRulesets = profile.FloatingWindowRowRulesets;
        insertIndex = Math.Clamp(insertIndex, 0, FloatingTriggerRows.Count);
        var newRowRuleset = new RowRulesetConfig();
        rowRulesets.Insert(insertIndex, newRowRuleset);
        var newRow = new FloatingTriggerRow
        {
            RowIndex = insertIndex + 1,
            RowRuleset = newRowRuleset
        };
        newRow.RowRuleset.PropertyChanged += OnRowRulesetPropertyChanged;
        if (newRow.RowRuleset.HidingRules is INotifyPropertyChanged rowHidingRules)
        {
            rowHidingRules.PropertyChanged += OnRowRulesetPropertyChanged;
        }
        FloatingTriggerRows.Insert(insertIndex, newRow);

        // 重新计算后续行的索引
        for (int i = insertIndex; i < FloatingTriggerRows.Count; i++)
        {
            FloatingTriggerRows[i].RowIndex = i + 1;
        }

        PersistFloatingTriggerRows();
    }

    public bool RemoveFloatingTriggerRow(FloatingTriggerRow row)
    {
        var index = FloatingTriggerRows.IndexOf(row);
        if (index < 0 || FloatingTriggerRows.Count <= 1)
        {
            return false;
        }

        // 注销被移除行的事件处理程序
        row.RowRuleset.PropertyChanged -= OnRowRulesetPropertyChanged;
        if (row.RowRuleset.HidingRules is INotifyPropertyChanged rowHidingRules)
        {
            rowHidingRules.PropertyChanged -= OnRowRulesetPropertyChanged;
        }

        // 按钮挪到相邻行，按钮级规则集配置的对象引用不变，事件监听继续有效
        var targetRow = index > 0 ? FloatingTriggerRows[index - 1] : FloatingTriggerRows[index + 1];
        foreach (var item in row.Buttons)
        {
            targetRow.Buttons.Add(item);
        }

        FloatingTriggerRows.RemoveAt(index);

        // 重新计算行索引
        for (int i = 0; i < FloatingTriggerRows.Count; i++)
        {
            FloatingTriggerRows[i].RowIndex = i + 1;
        }

        PersistFloatingTriggerRows();
        return true;
    }

    public bool MoveFloatingTrigger(string buttonId, int targetRowIndex, int targetIndex)
    {
        if (string.IsNullOrWhiteSpace(buttonId) || FloatingTriggerRows.Count == 0)
        {
            return false;
        }

        targetRowIndex = Math.Clamp(targetRowIndex, 0, FloatingTriggerRows.Count - 1);
        var sourceRow = FloatingTriggerRows.FirstOrDefault(r => r.Buttons.Any(b => b.ButtonId == buttonId));

        // 如果按钮不在任何行中（如在按钮池中），尝试从按钮池添加
        if (sourceRow == null)
        {
            return AddTriggerFromPool(buttonId, targetRowIndex, targetIndex);
        }

        var item = sourceRow.Buttons.First(b => b.ButtonId == buttonId);
        var sourceIndex = sourceRow.Buttons.IndexOf(item);
        var destinationRow = FloatingTriggerRows[targetRowIndex];

        if (ReferenceEquals(sourceRow, destinationRow))
        {
            if (targetIndex > sourceIndex)
            {
                targetIndex--;
            }
            targetIndex = Math.Clamp(targetIndex, 0, destinationRow.Buttons.Count - 1);
            if (targetIndex == sourceIndex)
            {
                return false;
            }

            sourceRow.Buttons.Move(sourceIndex, targetIndex);
            PersistFloatingTriggerRows();
            return true;
        }

        sourceRow.Buttons.RemoveAt(sourceIndex);
        targetIndex = Math.Clamp(targetIndex, 0, destinationRow.Buttons.Count);
        destinationRow.Buttons.Insert(targetIndex, item);
        PersistFloatingTriggerRows();
        return true;
    }

    /// <summary>
    /// 将已注册但不在行中的按钮添加到指定行（用于拖拽等场景）
    /// </summary>
    public bool AddTriggerFromPool(string buttonId, int targetRowIndex, int targetIndex)
    {
        if (string.IsNullOrWhiteSpace(buttonId) || FloatingTriggerRows.Count == 0)
        {
            return false;
        }

        var entry = _floatingWindowService.Entries.FirstOrDefault(e => e.ButtonId == buttonId);
        if (entry == null)
        {
            return false;
        }

        targetRowIndex = Math.Clamp(targetRowIndex, 0, FloatingTriggerRows.Count - 1);
        var destinationRow = FloatingTriggerRows[targetRowIndex];
        targetIndex = Math.Clamp(targetIndex, 0, destinationRow.Buttons.Count);

        var profile = CurrentFloatingWindowProfile;
        if (!profile.FloatingWindowButtonRulesets.TryGetValue(buttonId, out var btnConfig))
        {
            btnConfig = new ButtonRulesetConfig();
            profile.FloatingWindowButtonRulesets[buttonId] = btnConfig;
        }
        // 重新添加到行中时恢复可见
        btnConfig.IsVisible = true;

        var item = new FloatingTriggerItem
        {
            ButtonId = entry.ButtonId,
            Icon = entry.Icon,
            ButtonName = entry.LayoutName,
            Config = btnConfig
        };
        item.Config.PropertyChanged += OnButtonConfigPropertyChanged;
        if (item.Config.HidingRules is INotifyPropertyChanged btnHidingRules)
        {
            btnHidingRules.PropertyChanged += OnButtonConfigPropertyChanged;
        }

        destinationRow.Buttons.Insert(targetIndex, item);
        PersistFloatingTriggerRows();
        return true;
    }

    public void PersistFloatingTriggerRows(bool updateWindow = true, bool forceSave = true)
    {
        var profile = CurrentFloatingWindowProfile;
        var newRows = FloatingTriggerRows
            .Select(row => row.Buttons.Select(x => x.ButtonId).ToList())
            .ToList();
        var newOrder = newRows
            .SelectMany(row => row)
            .ToList();

        var rowsChanged = !AreRowsEqual(profile.FloatingWindowButtonRows, newRows);
        var orderChanged = !(profile.FloatingWindowButtonOrder ?? []).SequenceEqual(newOrder);

        if (rowsChanged)
        {
            profile.FloatingWindowButtonRows = newRows;
        }

        if (orderChanged)
        {
            profile.FloatingWindowButtonOrder = newOrder;
        }

        // 同步行规则集：确保 FloatingWindowRowRulesets 与行数一致
        var rowRulesets = profile.FloatingWindowRowRulesets;
        while (rowRulesets.Count < FloatingTriggerRows.Count)
        {
            rowRulesets.Add(new RowRulesetConfig());
        }
        while (rowRulesets.Count > FloatingTriggerRows.Count)
        {
            // 注销被移除行规则集的事件
            var removedRowRuleset = rowRulesets[rowRulesets.Count - 1];
            removedRowRuleset.PropertyChanged -= OnRowRulesetPropertyChanged;
            if (removedRowRuleset.HidingRules is INotifyPropertyChanged removedHidingRules)
            {
                removedHidingRules.PropertyChanged -= OnRowRulesetPropertyChanged;
            }
            rowRulesets.RemoveAt(rowRulesets.Count - 1);
        }
        // 同步每行的 RowRuleset 引用（确保ViewModel中的修改反映到profile）
        for (int i = 0; i < FloatingTriggerRows.Count; i++)
        {
            var vmRow = FloatingTriggerRows[i];
            if (!ReferenceEquals(vmRow.RowRuleset, rowRulesets[i]))
            {
                // RowRuleset 引用变更时，重新注册事件
                vmRow.RowRuleset.PropertyChanged -= OnRowRulesetPropertyChanged;
                if (vmRow.RowRuleset.HidingRules is INotifyPropertyChanged oldHidingRules)
                {
                    oldHidingRules.PropertyChanged -= OnRowRulesetPropertyChanged;
                }
                vmRow.RowRuleset = rowRulesets[i];
                vmRow.RowRuleset.PropertyChanged += OnRowRulesetPropertyChanged;
                if (vmRow.RowRuleset.HidingRules is INotifyPropertyChanged newHidingRules)
                {
                    newHidingRules.PropertyChanged += OnRowRulesetPropertyChanged;
                }
            }
        }

        // 清理不再使用的按钮规则集配置
        var usedButtonIds = new HashSet<string>(newOrder);
        var staleButtonIds = profile.FloatingWindowButtonRulesets.Keys.Where(id => !usedButtonIds.Contains(id)).ToList();
        foreach (var staleId in staleButtonIds)
        {
            profile.FloatingWindowButtonRulesets.Remove(staleId);
        }

        if (forceSave)
        {
            _floatingWindowService.ProfileManager.SaveProfile();
        }

        if (updateWindow)
        {
            _floatingWindowService.UpdateWindowState();
        }
    }

    public void AddFloatingWindowProfile(string? name = null)
    {
        var newName = _floatingWindowService.ProfileManager.CreateProfile(name);
        RefreshFloatingWindowProfiles();
        SelectedFloatingWindowProfile = newName;
        SwitchFloatingWindowProfile(newName);
    }

    public void RemoveFloatingWindowProfile(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            return;
        }

        if (_floatingWindowService.ProfileManager.RemoveProfile(profileName))
        {
            RefreshFloatingWindowProfiles();
            // 如果删除的是当前方案，切换到 Default
            if (string.Equals(SelectedFloatingWindowProfile, profileName, StringComparison.OrdinalIgnoreCase))
            {
                SwitchFloatingWindowProfile("Default");
            }
        }
    }

    public void SwitchFloatingWindowProfile(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            return;
        }

        _floatingWindowService.SwitchToProfile(profileName);
        SelectedFloatingWindowProfile = profileName;
        RefreshFloatingTriggers();

        // 通知 UI 重新注册 Profile 属性变更事件监听
        ProfileChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Profile 对象发生变化时触发（切换方案后需要重新注册事件监听）
    /// </summary>
    public event EventHandler? ProfileChanged;

    public void Dispose()
    {
        _floatingWindowService.EntriesChanged -= _entriesChangedHandler;
    }

    private static bool AreRowsEqual(IReadOnlyList<List<string>>? left, IReadOnlyList<List<string>> right)
    {
        if (left == null)
        {
            return right.Count == 0;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (!left[i].SequenceEqual(right[i]))
            {
                return false;
            }
        }

        return true;
    }
}

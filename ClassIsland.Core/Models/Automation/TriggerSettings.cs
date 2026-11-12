using System.Text.Json.Serialization;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.Core.Models.Automation;

/// <summary>
/// 代表一个触发器的设置。
/// </summary>
public class TriggerSettings : ObservableRecipient
{
    private string _id = "";
    private object? _settings;

    /// <summary>
    /// 触发器 ID
    /// </summary>
    public string Id
    {
        get => _id;
        set
        {
            if (value == _id) return;
            _id = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AssociatedTriggerInfo));
            OnPropertyChanged(nameof(TriggerDisplayName));
            OnPropertyChanged(nameof(TriggerDisplayIconSource));
        }
    }

    /// <summary>
    /// 触发器设置
    /// </summary>
    public object? Settings
    {
        get => _settings;
        set
        {
            if (Equals(value, _settings)) return;
            _settings = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 关联的触发器信息
    /// </summary>
    [JsonIgnore]
    public TriggerInfo? AssociatedTriggerInfo => IAutomationService.RegisteredTriggers.FirstOrDefault(x => x.Id == Id);

    /// <summary>
    /// 触发器行按钮的显示名；未选择类型时给出占位提示。
    /// </summary>
    [JsonIgnore]
    public string TriggerDisplayName => AssociatedTriggerInfo?.Name ?? "选择触发器…";

    /// <summary>
    /// 触发器行按钮的显示图标；未选择类型时为 null。
    /// </summary>
    [JsonIgnore]
    public FluentAvalonia.UI.Controls.FAIconSource? TriggerDisplayIconSource => AssociatedTriggerInfo?.IconSource;

    [JsonIgnore]
    internal TriggerBase? TriggerInstance { get; set; }

    internal void Unload()
    {
        Unloading?.Invoke(this, EventArgs.Empty);
    }

    internal event EventHandler? Unloading;
}

using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using ClassIsland.Core.Models.Ruleset;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.Models.SystemTools;

/// <summary>
/// 悬浮窗配置方案，仅保存悬浮窗按钮布局以及按钮/行规则集。
/// 注意：外观、位置、层级、显示状态和整窗规则集是全局设置，不随方案切换。
/// </summary>
public partial class FloatingWindowProfile : ObservableObject
{
    [ObservableProperty]
    [JsonPropertyName("name")]
    private string _name = "Default";

    [ObservableProperty]
    [JsonPropertyName("floatingWindowHorizontal")]
    private bool _floatingWindowHorizontal;

    [JsonPropertyName("floatingWindowButtonOrder")]
    public List<string> FloatingWindowButtonOrder { get; set; } = new();

    [JsonPropertyName("floatingWindowButtonRows")]
    public List<List<string>> FloatingWindowButtonRows { get; set; } = new();

    [JsonPropertyName("floatingWindowButtonRulesets")]
    public Dictionary<string, ButtonRulesetConfig> FloatingWindowButtonRulesets { get; set; } = new();

    [JsonPropertyName("floatingWindowRowRulesets")]
    public List<RowRulesetConfig> FloatingWindowRowRulesets { get; set; } = new();

    /// <summary>
    /// 清理不存在的按钮ID，返回是否有变更
    /// </summary>
    public bool PruneInvalidButtonIds(IEnumerable<string> validButtonIds)
    {
        var validSet = validButtonIds.ToHashSet();
        var changed = false;

        var newOrder = FloatingWindowButtonOrder.Where(id => validSet.Contains(id)).ToList();
        if (newOrder.Count != FloatingWindowButtonOrder.Count)
        {
            FloatingWindowButtonOrder = newOrder;
            changed = true;
        }

        var newRows = FloatingWindowButtonRows
            .Select(row => row.Where(id => validSet.Contains(id)).ToList())
            .ToList();
        if (newRows.Count != FloatingWindowButtonRows.Count ||
            newRows.Zip(FloatingWindowButtonRows, (a, b) => a.SequenceEqual(b)).Any(x => !x))
        {
            FloatingWindowButtonRows = newRows;
            changed = true;
        }

        var invalidButtonConfigs = FloatingWindowButtonRulesets.Keys.Where(id => !validSet.Contains(id)).ToList();
        foreach (var id in invalidButtonConfigs)
        {
            FloatingWindowButtonRulesets.Remove(id);
            changed = true;
        }

        return changed;
    }
}

/// <summary>
/// 悬浮窗按钮的规则集配置
/// </summary>
public partial class ButtonRulesetConfig : ObservableObject
{
    [ObservableProperty]
    [JsonPropertyName("isVisible")]
    private bool _isVisible = true;

    [ObservableProperty]
    [JsonPropertyName("hideOnRule")]
    private bool _hideOnRule;

    [ObservableProperty]
    [JsonPropertyName("hidingRules")]
    private Ruleset _hidingRules = new();
}

/// <summary>
/// 悬浮窗行的规则集配置
/// </summary>
public partial class RowRulesetConfig : ObservableObject
{
    [ObservableProperty]
    [JsonPropertyName("isVisible")]
    private bool _isVisible = true;

    [ObservableProperty]
    [JsonPropertyName("hideOnRule")]
    private bool _hideOnRule;

    [ObservableProperty]
    [JsonPropertyName("hidingRules")]
    private Ruleset _hidingRules = new();
}

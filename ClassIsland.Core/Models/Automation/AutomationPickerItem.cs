using ClassIsland.Core.Models.Automation;
using FluentAvalonia.UI.Controls;

namespace ClassIsland.Core.Models.Automation;

/// <summary>
/// 自动化选择器面板中的单个可选项。
/// 触发器条目来自触发器注册表；行动条目来自行动菜单树拍平结果，
/// 保留同 ID 多预设变体（如“运行”的五种预设）与插件挂载的条目。
/// </summary>
public class AutomationPickerItem
{
    /// <summary>
    /// 触发器/行动注册 ID。
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 显示名称。
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 图标源。
    /// </summary>
    public FAIconSource? IconSource { get; init; }

    /// <summary>
    /// 分类名。来自 <see cref="ClassIsland.Core.Attributes.AutomationCategoryAttribute"/>，
    /// 未打标时由注册链路回退（“内置”/“插件提供”）。
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// 行动预设对应的菜单树节点；触发器条目为 null。
    /// 落地时经它保留预设设置（ActionItemSettingsSetter）。
    /// </summary>
    public ActionMenuTreeItem? MenuSource { get; init; }
}

/// <summary>
/// 选择器面板当前选择的目标类型。
/// </summary>
public enum AutomationPickerKind
{
    /// <summary>
    /// 正在选择触发器。
    /// </summary>
    Trigger,

    /// <summary>
    /// 正在选择行动。
    /// </summary>
    Action
}

/// <summary>
/// 一次选择器面板打开请求。
/// </summary>
public class AutomationPickRequest
{
    /// <summary>
    /// 打开时的初始模式。
    /// </summary>
    public AutomationPickerKind Kind { get; set; } = AutomationPickerKind.Trigger;

    /// <summary>
    /// 是否锁定模式（换类型等定向场景）。锁定后右栏模式按钮不可用，
    /// 选中项一律经 <see cref="Callback"/> 落地，不走宿主投递。
    /// </summary>
    public bool LockMode { get; set; }

    /// <summary>
    /// 定向落地回调；null 时由宿主的投递委托按当前模式落地。
    /// </summary>
    public Action<AutomationPickerItem>? Callback { get; set; }

    /// <summary>
    /// 定向场景的“已存在 ID”来源；null 时使用宿主提供的按模式集合。
    /// </summary>
    public Func<IEnumerable<string>>? ExistingIdsProvider { get; set; }
}

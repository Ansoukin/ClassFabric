using ClassIsland.Core.Models.Automation;
using ClassIsland.Shared.Models.Automation;

namespace ClassIsland.Core.Controls.Automation;

/// <summary>
/// 选择面板选中结果的落地辅助。
/// </summary>
public static class AutomationPickerDelivery
{
    /// <summary>
    /// 依据菜单树节点创建行动项，保留预设设置（<see cref="ActionMenuTreeItem{TSettings}.ActionItemSettingsSetter"/>）。
    /// 行动菜单树里同一个行动 ID 可能有多个预设变体（如“运行”的应用程序/命令/文件等），
    /// 预设差异全在这里落地。
    /// </summary>
    public static ActionItem CreateActionItem(ActionMenuTreeItem menu)
    {
        var actionItem = new ActionItem { Id = menu.ActionItemId };

        if (menu.GetType().IsGenericType &&
            menu.GetType().GetGenericTypeDefinition() == typeof(ActionMenuTreeItem<>))
        {
            var settingsType = menu.GetType().GetGenericArguments()[0];
            var settings = Activator.CreateInstance(settingsType);

            var setterProperty = menu.GetType()
                .GetProperty(nameof(ActionMenuTreeItem<object>.ActionItemSettingsSetter));
            var setter = setterProperty?.GetValue(menu) as Delegate;
            setter?.DynamicInvoke(settings);
            actionItem.Settings = settings;
        }

        actionItem.IsNewAdded = true;
        return actionItem;
    }
}

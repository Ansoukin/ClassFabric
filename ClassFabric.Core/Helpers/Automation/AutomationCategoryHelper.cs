using ClassIsland.Core.Attributes;

namespace ClassIsland.Core.Helpers.Automation;

/// <summary>
/// 提供自动化分类解析方法。
/// </summary>
public static class AutomationCategoryHelper
{
    /// <summary>分类为内置自动化项。</summary>
    public const string BuiltInCategory = "内置";
    /// <summary>分类为插件提供的自动化项。</summary>
    public const string PluginCategory = "插件提供";
    /// <summary>分类为未分类自动化项。</summary>
    public const string UncategorizedCategory = "未分类";

    /// <summary>
    /// 解析自动化类型的分类。
    /// </summary>
    public static string Resolve(Type automationType, string id, string? defaultGroupToMenu = null)
    {
        if (automationType.GetCustomAttributes(false)
                .FirstOrDefault(x => x is AutomationCategoryAttribute) is AutomationCategoryAttribute category &&
            !string.IsNullOrWhiteSpace(category.Category))
            return category.Category;

        if (!string.IsNullOrWhiteSpace(defaultGroupToMenu))
            return defaultGroupToMenu;

        if (!string.IsNullOrWhiteSpace(id))
            return id.StartsWith("classfabric.", StringComparison.OrdinalIgnoreCase)
                ? BuiltInCategory
                : PluginCategory;

        return UncategorizedCategory;
    }
}

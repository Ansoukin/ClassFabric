namespace ClassIsland.Core.Attributes;

/// <summary>
/// 指定自动化动作或触发器的分类。
/// </summary>
/// <param name="category">分类名称。</param>
[AttributeUsage(AttributeTargets.Class)]
public class AutomationCategoryAttribute(string category) : Attribute
{
    /// <summary>
    /// 分类名称。
    /// </summary>
    public string Category { get; } = category;
}

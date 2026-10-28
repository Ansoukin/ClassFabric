using System;
using ClassIsland.Core.Attributes;

namespace ClassIsland.Models;

/// <summary>
/// 设置项搜索结果条目。PageInfo 指向所属设置页，ControlKey 是页面内目标控件的 x:Name，
/// 跳转后由设置窗口定位并滚动到该控件。
/// </summary>
public class SettingsSearchItem(SettingsPageInfo pageInfo, string title, string? description, string? controlKey)
{
    public SettingsPageInfo PageInfo { get; } = pageInfo;
    public string Title { get; } = title;
    public string? Description { get; } = description;
    public string? ControlKey { get; } = controlKey;

    public bool Matches(string query)
    {
        return Title.Contains(query, StringComparison.OrdinalIgnoreCase)
               || (Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
               || PageInfo.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
               || PageInfo.Id.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using ClassIsland.Core.Models.Plugin;

namespace ClassIsland.Core.Services;

/// <summary>
/// 内置插件登记表。随应用分发的插件注册成功后在此登记，供插件页面的内置列表展示，
/// 以及插件市场对已内置插件做覆盖提示查询。
/// </summary>
public static class EmbeddedPluginRegistry
{
    /// <summary>
    /// 已注册的内置插件信息。
    /// </summary>
    public static ObservableCollection<PluginInfo> BuiltInPlugins { get; } = [];

    /// <summary>
    /// 按插件 ID 查找内置插件。
    /// </summary>
    /// <param name="id">插件 ID</param>
    /// <param name="info">命中的内置插件信息</param>
    /// <returns>是否命中</returns>
    public static bool TryGetById(string id, [NotNullWhen(true)] out PluginInfo? info)
    {
        info = BuiltInPlugins.FirstOrDefault(x => x.Manifest.Id == id);
        return info != null;
    }
}

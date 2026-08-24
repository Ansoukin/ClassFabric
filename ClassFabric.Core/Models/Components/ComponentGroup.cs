using ClassFabric.Core.Attributes;
using ClassFabric.Core.Models.Plugin;

namespace ClassFabric.Core.Models.Components;

/// <summary>
/// 组件库分组。每个分组对应一个组件来源（内置或某个插件）。
/// </summary>
public record ComponentGroup(
    string GroupName,
    PluginInfo? Plugin,
    IReadOnlyList<ComponentInfo> Components);
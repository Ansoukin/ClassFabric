using ClassFabric.Core.Models.Plugin;

namespace ClassFabric.Models.Plugins;

public record class DependencyNode(PluginInfo Plugin) 
{
    public PluginInfo Plugin { get; set; } = Plugin;

    public int DependencyTreeDepth { get; set; } = 0;

    public bool IsDiscovered { get; set; } = false;
}
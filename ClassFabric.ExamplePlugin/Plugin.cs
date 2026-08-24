using ClassFabric.Core.Abstractions;
using ClassFabric.Core.Attributes;
using ClassFabric.Core.Extensions.Registry;
using ClassFabric.ExamplePlugin.Views.SettingsPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClassFabric.ExamplePlugin;

[PluginEntrance]
public class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        Console.WriteLine("Hello world!");
        services.AddSettingsPage<HelloSettingsPage>();
    }
}
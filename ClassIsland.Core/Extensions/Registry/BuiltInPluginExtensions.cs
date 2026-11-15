using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Enums;
using ClassIsland.Core.Models.Plugin;
using ClassIsland.Core.Services;
using ClassIsland.Core.Services.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClassIsland.Core.Extensions.Registry;

/// <summary>
/// 注册内置插件的 <see cref="IServiceCollection"/> 扩展。
/// </summary>
public static class BuiltInPluginExtensions
{
    /// <summary>
    /// 注册一个随应用分发的内置插件。外部插件目录存在同 ID 且已成功加载的社区版时，
    /// 内置版让位（不注册服务、不进内置列表），用户可安装社区版追新而不必等发版；
    /// 社区版被卸载或禁用后，内置版下次启动自动接管。
    /// </summary>
    /// <param name="services"><see cref="IServiceCollection"/>实例</param>
    /// <param name="context">宿主 <see cref="HostBuilderContext"/></param>
    /// <param name="info">内置插件的 PluginInfo（Manifest 需至少填 Id/Name/Description/Version/Author）</param>
    /// <param name="configure">宿主能力委托配置（设置读取、主题取色等）</param>
    /// <typeparam name="TPlugin">插件入口类型</typeparam>
    /// <returns>原来的<see cref="IServiceCollection"/>实例</returns>
    /// <remarks>
    /// 必须在 <c>PluginService.InitializePlugins</c> 之后调用：让位判断要求外部插件已完成加载枚举，
    /// 否则看不到同 ID 的社区版。
    /// </remarks>
    public static IServiceCollection AddBuiltInPlugin<TPlugin>(
        this IServiceCollection services,
        HostBuilderContext context,
        PluginInfo info,
        Action<BuiltInPluginConfig>? configure = null)
        where TPlugin : PluginBase, new()
    {
        var id = info.Manifest.Id;
        // 只认成功加载的社区版：禁用/加载失败的社区版占着 ID 不算数，内置版照常接管。
        if (IPluginService.LoadedPluginsInternal.Any(x => x.Manifest.Id == id && x.LoadStatus == PluginLoadStatus.Loaded))
        {
            Console.WriteLine($"检测到插件 {id} 已由外部插件版本加载，内置版本本次让位。");
            return services;
        }

        var plugin = new TPlugin();
        info.IsBuiltIn = true;
        info.LoadStatus = PluginLoadStatus.Loaded;
        plugin.Info = info;
        // 内置插件配置与社区版隔离存放（Config/Plugins/BuildIn/<id>/），两版切换时各自保留。
        plugin.PluginConfigFolder = Path.Combine(CommonDirectories.AppConfigPath, "Plugins", "BuildIn", id);
        if (!Directory.Exists(plugin.PluginConfigFolder))
        {
            Directory.CreateDirectory(plugin.PluginConfigFolder);
        }
        plugin.BuiltInConfig = new BuiltInPluginConfig();
        configure?.Invoke(plugin.BuiltInConfig);
        EmbeddedPluginRegistry.BuiltInPlugins.Add(info);

        // 与外部插件同款：Initialize 期间标记当前注册插件，期间注册的组件可归因到插件。
        var previousPlugin = ComponentRegistryService.CurrentRegisteringPlugin.Value;
        ComponentRegistryService.CurrentRegisteringPlugin.Value = info;
        try
        {
            plugin.Initialize(context, services);
        }
        finally
        {
            ComponentRegistryService.CurrentRegisteringPlugin.Value = previousPlugin;
        }
        services.AddSingleton(typeof(PluginBase), plugin);
        services.AddSingleton(plugin);
        return services;
    }
}

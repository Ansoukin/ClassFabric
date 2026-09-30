using ClassFabric.RemoteBroadcast.Services;
using ClassFabric.RemoteBroadcast.UI;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClassFabric.RemoteBroadcast;

/// <summary>
/// Remote Broadcast 插件入口：注册设置页与后台服务。M0 阶段仅搭建骨架，HTTP 服务在 M1 接入。
/// </summary>
public class RemoteBroadcastPlugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        services.AddSettingsPage<RemoteBroadcastSettingsPage>();
        services.AddSingleton<RemoteBroadcastService>();
        services.AddHostedService(sp => sp.GetRequiredService<RemoteBroadcastService>());

        AppBase.Current.AppStarted += (_, _) =>
        {
            var logger = IAppHost.GetService<ILogger<RemoteBroadcastPlugin>>();
            logger?.LogInformation("[RemoteBroadcast] 插件已加载（M0 骨架），Quick Call 服务将在 M1 接入");
        };
    }
}

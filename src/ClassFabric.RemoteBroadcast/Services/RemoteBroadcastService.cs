using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClassFabric.RemoteBroadcast.Services;

/// <summary>
/// 远程广播主服务：M0 为生命周期占位，M1 在此挂载 HttpListener 服务端与 Quick Call 状态机。
/// </summary>
public class RemoteBroadcastService(ILogger<RemoteBroadcastService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("[RemoteBroadcast] 后台服务已启动（M0 骨架，HTTP 服务端于 M1 接入）");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("[RemoteBroadcast] 后台服务已停止");
        return Task.CompletedTask;
    }
}

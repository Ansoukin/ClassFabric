using ClassIsland.Core.Abstractions.Services.NotificationProviders;
using ClassIsland.Core.Attributes;

namespace ClassFabric.RemoteBroadcast.Services;

/// <summary>
/// Quick Call 提醒提供方：本身不产生提醒，只作为宿主提醒系统的注册身份存在，
/// 真正的发送由 <see cref="QuickCallService"/> 通过基类的 ShowNotificationAsync 完成。
/// </summary>
[NotificationProviderInfo(
    "6F2C9D34-8A51-4B7E-9C04-D1E2F3A50678",
    "远程广播",
    "fluent(\uEB70)",
    "远程广播插件的 Quick Call 呼叫提醒")]
public class QuickCallNotificationProvider : NotificationProviderBase
{
}

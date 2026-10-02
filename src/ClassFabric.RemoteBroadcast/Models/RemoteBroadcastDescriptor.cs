namespace ClassFabric.RemoteBroadcast.Models;

/// <summary>
/// 插件身份信息，给「关于」页用。
/// 版本号在 Initialize 里从清单读一次就定下来：设置页是运行时按需创建的，让页面自己去摸
/// PluginBase.Info 既拿不到（没有引用），也会把「插件元数据」和「页面」耦合在一起。
/// </summary>
public sealed record RemoteBroadcastDescriptor(string Version);

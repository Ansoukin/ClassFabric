namespace ClassIsland.Core.Abstractions.Services;

/// <summary>
/// 涟漪特效播放入口。提醒管线在特效风格选为涟漪时经此触发 FancyAnima 的 GDI 涟漪，
/// 与 <see cref="IFancyAnimaSettings"/> 分离：一个管设置读写，一个管播放动作。
/// 非 Windows 平台不会注册此服务，调用方以此判断涟漪是否可用。
/// </summary>
public interface INotificationRippleEffect
{
    /// <summary>
    /// 播放一次涟漪特效。上一段动画未播完时忽略本次请求。
    /// </summary>
    Task PlayRippleAsync();
}

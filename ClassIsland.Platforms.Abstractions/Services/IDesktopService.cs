namespace ClassIsland.Platforms.Abstraction.Services;

/// <summary>
/// 桌面相关服务。
/// </summary>
public interface IDesktopService
{
    /// <summary>
    /// 已启用开机自启。
    /// </summary>
    public bool IsAutoStartEnabled { get; set; }

    /// <summary>
    /// 已启用管理员自启动（登录时以管理员身份启动，仅部分平台支持）。
    /// </summary>
    public bool IsAdminStartupEnabled { get; set; }

    /// <summary>
    /// 当前进程以管理员身份运行。
    /// </summary>
    public bool IsRunningAsAdmin { get; }
    
    /// <summary>
    /// 已注册 Url 协议。
    /// </summary>
    public bool IsUrlSchemeRegistered { get; set; }
}

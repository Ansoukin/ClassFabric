namespace ClassIsland.Platforms.Abstraction.Services;

/// <summary>
/// 系统热键服务。
/// RegisterHotKey 是全局热键注册，必须挂在有消息泵的窗口上，因此只有 Windows 桌面端有真实现；
/// 其余平台为 stub（注册返回 -1、永不触发），调用方按失败处理即可。
/// </summary>
public interface ISystemHotkeyService
{
    /// <summary>
    /// 热键按下事件。事件在应用 UI 线程上发出。
    /// </summary>
    event EventHandler<SystemHotkeyEventArgs>? HotkeyPressed;

    /// <summary>
    /// 注册全局热键。相同组合重复注册时复用同一热键 ID（引用计数）。
    /// 注册失败（组合被其它程序占用等）抛 Win32Exception。
    /// </summary>
    /// <param name="modifierKeys">RegisterHotKey 的 MOD_* 组合：0x0001=Alt 0x0002=Ctrl 0x0004=Shift 0x0008=Win</param>
    /// <param name="virtualKey">虚拟键码</param>
    /// <returns>热键 ID，注销时传回</returns>
    int RegisterHotkey(int modifierKeys, uint virtualKey);

    /// <summary>
    /// 注销热键（引用计数减一，减到零才真正注销）。
    /// </summary>
    void UnregisterHotkey(int hotkeyId);

    /// <summary>
    /// 该组合当前是否已注册。
    /// </summary>
    bool IsHotkeyRegistered(int modifierKeys, uint virtualKey);

    /// <summary>
    /// 临时注销全部热键（录制新热键时用，否则录制的组合会被系统拦截到本进程）。
    /// </summary>
    void SuspendAllHotkeys();

    /// <summary>
    /// 恢复被挂起的热键注册。
    /// </summary>
    void ResumeAllHotkeys();

    /// <summary>
    /// 生成「Ctrl+Alt+F9」样式的显示文本。
    /// </summary>
    string GetHotkeyDisplay(int modifierKeys, uint virtualKey);
}

/// <summary>
/// 热键按下事件参数。
/// </summary>
public class SystemHotkeyEventArgs : EventArgs
{
    /// <summary>修饰键组合（MOD_* 位标志）。</summary>
    public int ModifierKeys { get; set; }

    /// <summary>虚拟键码。</summary>
    public uint VirtualKey { get; set; }

    /// <summary>热键 ID。</summary>
    public int HotkeyId { get; set; }
}

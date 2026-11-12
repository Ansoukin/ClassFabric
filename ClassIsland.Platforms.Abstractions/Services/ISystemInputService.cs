namespace ClassIsland.Platforms.Abstraction.Services;

/// <summary>
/// 鼠标按键种类。
/// </summary>
public enum SystemMouseButton
{
    /// <summary>鼠标左键。</summary>
    Left,
    /// <summary>鼠标右键。</summary>
    Right,
    /// <summary>鼠标中键。</summary>
    Middle
}

/// <summary>
/// 系统键鼠模拟与输入空闲查询。
/// 仅 Windows 桌面端有真实现，其余平台为空操作 stub；调用方无需再判平台。
/// </summary>
public interface ISystemInputService
{
    /// <summary>
    /// 按下虚拟键。与 <see cref="KeyUp"/> 配对使用，组合键按「全部按下再逆序释放」的顺序发。
    /// </summary>
    void KeyDown(ushort virtualKey);

    /// <summary>
    /// 释放虚拟键。
    /// </summary>
    void KeyUp(ushort virtualKey);

    /// <summary>
    /// 按下鼠标键。
    /// </summary>
    void MouseDown(SystemMouseButton button);

    /// <summary>
    /// 释放鼠标键。
    /// </summary>
    void MouseUp(SystemMouseButton button);

    /// <summary>
    /// 滚动滚轮，正值向上。
    /// </summary>
    void MouseWheel(int delta);

    /// <summary>
    /// 移动光标到屏幕绝对坐标。
    /// </summary>
    bool MoveMouseCursor(int x, int y);

    /// <summary>
    /// 距上次键鼠输入经过的毫秒数。不支持空闲查询的平台返回 0。
    /// </summary>
    uint GetIdleMilliseconds();
}

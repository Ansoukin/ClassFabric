using System.Runtime.InteropServices;
using ClassIsland.Platforms.Abstraction.Services;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace ClassIsland.Platform.Windows.Services;

/// <summary>
/// Windows 端键鼠模拟实现。全部走 user32 的 keybd_event/mouse_event，与键鼠钩子、注入标记等低级机制兼容。
/// </summary>
public class SystemInputService : ISystemInputService
{
    /// <inheritdoc />
    public void KeyDown(ushort virtualKey) =>
        PInvoke.keybd_event((byte)virtualKey, 0, 0, 0);

    /// <inheritdoc />
    public void KeyUp(ushort virtualKey) =>
        PInvoke.keybd_event((byte)virtualKey, 0, KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP, 0);

    /// <inheritdoc />
    public void MouseDown(SystemMouseButton button) =>
        PInvoke.mouse_event(DownFlag(button), 0, 0, 0, 0);

    /// <inheritdoc />
    public void MouseUp(SystemMouseButton button) =>
        PInvoke.mouse_event(UpFlag(button), 0, 0, 0, 0);

    /// <inheritdoc />
    public void MouseWheel(int delta) =>
        PInvoke.mouse_event(MOUSE_EVENT_FLAGS.MOUSEEVENTF_WHEEL, 0, 0, delta, 0);

    /// <inheritdoc />
    public bool MoveMouseCursor(int x, int y) =>
        PInvoke.SetCursorPos(x, y);

    /// <inheritdoc />
    public uint GetIdleMilliseconds()
    {
        var info = new LASTINPUTINFO
        {
            cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>()
        };
        if (!PInvoke.GetLastInputInfo(ref info))
        {
            return 0;
        }

        // GetTickCount 语义：uint 相减天然处理回绕，不能改成 long 差值。
        return unchecked((uint)Environment.TickCount - info.dwTime);
    }

    static MOUSE_EVENT_FLAGS DownFlag(SystemMouseButton button) => button switch
    {
        SystemMouseButton.Left => MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTDOWN,
        SystemMouseButton.Right => MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTDOWN,
        _ => MOUSE_EVENT_FLAGS.MOUSEEVENTF_MIDDLEDOWN
    };

    static MOUSE_EVENT_FLAGS UpFlag(SystemMouseButton button) => button switch
    {
        SystemMouseButton.Left => MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTUP,
        SystemMouseButton.Right => MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTUP,
        _ => MOUSE_EVENT_FLAGS.MOUSEEVENTF_MIDDLEUP
    };
}

using System.Collections.Generic;

namespace ClassIsland.Controls.ActionSettingsControls.SystemTools;

/// <summary>
/// 虚拟键码到按键名的转换。录制钩子只拿得到 Win32 虚拟键码，界面要显示人能看懂的名字。
/// 键名参考通用 Win32 键名，与录制数据里的旧格式保持一致的可读性；未收录的键码原样返回数字。
/// </summary>
public static class VirtualKeyNameHelper
{
    private static readonly Dictionary<uint, string> Names = new()
    {
        [0x01] = "LeftMouse", [0x02] = "RightMouse", [0x04] = "MiddleMouse",
        [0x08] = "Back", [0x09] = "Tab", [0x0C] = "Clear", [0x0D] = "Enter",
        [0x10] = "Shift", [0x11] = "Ctrl", [0x12] = "Alt", [0x13] = "Pause",
        [0x14] = "CapsLock", [0x1B] = "Esc", [0x20] = "Space",
        [0x21] = "PageUp", [0x22] = "PageDown", [0x23] = "End", [0x24] = "Home",
        [0x25] = "Left", [0x26] = "Up", [0x27] = "Right", [0x28] = "Down",
        [0x29] = "Select", [0x2A] = "Print", [0x2C] = "PrintScreen",
        [0x2D] = "Insert", [0x2E] = "Delete", [0x2F] = "Help",
        [0x5B] = "LWin", [0x5C] = "RWin", [0x5F] = "Sleep",
        [0x6A] = "Multiply", [0x6B] = "Add", [0x6C] = "Separator",
        [0x6D] = "Subtract", [0x6E] = "Decimal", [0x6F] = "Divide",
        [0x90] = "NumLock", [0x91] = "ScrollLock",
        [0xA0] = "LShift", [0xA1] = "RShift", [0xA2] = "LCtrl", [0xA3] = "RCtrl",
        [0xA4] = "LAlt", [0xA5] = "RAlt",
        [0xBA] = "OemSemicolon", [0xBB] = "OemPlus", [0xBC] = "OemComma",
        [0xBD] = "OemMinus", [0xBE] = "OemPeriod", [0xBF] = "OemQuestion",
        [0xC0] = "OemTilde", [0xDB] = "OemOpenBrackets", [0xDC] = "OemPipe",
        [0xDD] = "OemCloseBrackets", [0xDE] = "OemQuotes", [0xDF] = "Oem8",
        [0xE2] = "OemBackslash",
        [0xB0] = "MediaNext", [0xB1] = "MediaPrevious", [0xB2] = "MediaStop",
        [0xB3] = "MediaPlayPause", [0xB4] = "VolumeMute", [0xB5] = "VolumeDown",
        [0xB6] = "VolumeUp",
    };

    /// <summary>
    /// 取虚拟键码对应的按键名。数字、字母、小键盘与功能键按规律生成，其余查表。
    /// </summary>
    public static string GetKeyName(uint vkCode)
    {
        // 数字键 0-9 在 Win32 键码布局上与 '0'-'9' 连续。
        if (vkCode is >= 0x30 and <= 0x39)
            return ((char)vkCode).ToString();

        // 大写字母 A-Z 与键码布局连续。
        if (vkCode is >= 0x41 and <= 0x5A)
            return ((char)vkCode).ToString();

        // 小键盘数字 Numpad0-9。
        if (vkCode is >= 0x60 and <= 0x69)
            return $"Num{vkCode - 0x60}";

        // 功能键 F1-F24，0x70 起。
        if (vkCode is >= 0x70 and <= 0x87)
            return $"F{vkCode - 0x6F}";

        return Names.TryGetValue(vkCode, out var name) ? name : vkCode.ToString();
    }
}

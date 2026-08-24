using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;

namespace ClassFabric.Services.Automation.PlatformTools;

/// <summary>
/// 键鼠模拟与前台窗口操作的统一入口。所有 Win32 调用收敛在此，行动层不得直接触碰 P/Invoke。
/// </summary>
public partial class InputSimulationService
{
    // 预设快捷键表：名称 → (修饰键, 主键) 的虚拟键码序列
    private static readonly Dictionary<string, ushort[]> HotkeyPresets = new()
    {
        ["AltF4"] = [0x12, 0x25],
        ["AltTab"] = [0x12, 0x09],
        ["CtrlZ"] = [0x11, 0x5A],
        ["Enter"] = [0x0D],
        ["Esc"] = [0x1B],
    };

    public static IReadOnlyList<string> HotkeyPresetNames => HotkeyPresets.Keys.ToList();

    public virtual void SendHotkeyPreset(string preset)
    {
        if (!HotkeyPresets.TryGetValue(preset, out var keys))
            throw new ArgumentException($"未知的快捷键预设 {preset}。", nameof(preset));
        SendKeySequence(keys);
    }

    /// <summary>
    /// 依次按下再逆序抬起一组虚拟键，实现组合键效果。
    /// </summary>
    public virtual void SendKeySequence(IReadOnlyList<ushort> virtualKeys)
    {
        foreach (var key in virtualKeys)
            SendSingleKey(key, true);
        foreach (var key in virtualKeys.Reverse())
            SendSingleKey(key, false);
    }

    /// <summary>
    /// 以 Unicode 扫码方式逐字符键入文本，不依赖剪贴板与目标布局。
    /// </summary>
    public virtual void TypeText(string text)
    {
        foreach (var ch in text)
        {
            var inputs = new[] { MakeUnicodeInput(ch, true), MakeUnicodeInput(ch, false) };
            SendInputRaw(inputs);
        }
    }

    public virtual void SendMouseButton(int x, int y, bool rightButton)
    {
        var move = MakeMouseMove((uint)x, (uint)y);
        var down = MakeMouseButtonInput((uint)(rightButton ? 0x0010 : 0x0002));
        var up = MakeMouseButtonInput((uint)(rightButton ? 0x0020 : 0x0004));
        SendInputRaw([move, down, up]);
    }

    public virtual void SendMouseWheel(int delta)
    {
        var input = new NativeInterop.INPUT();
        input.type = 1; // INPUT_MOUSE
        input.Anonymous.mi = new NativeInterop.MOUSEINPUT
        {
            mouseData = (uint)delta,
            dwFlags = 0x0800, // MOUSEEVENTF_WHEEL
        };
        SendInputRaw([input]);
    }

    public virtual void MoveMouseTo(int x, int y)
    {
        SendInputRaw([MakeMouseMove((uint)x, (uint)y)]);
    }

    /// <summary>
    /// 对前台窗口执行常见操作。关闭走 WM_CLOSE，给目标程序自行保存退出的机会。
    /// </summary>
    public virtual void OperateForegroundWindow(string operation)
    {
        var hwnd = NativeInterop.GetForegroundWindow();
        if (hwnd == default) return;
        switch (operation)
        {
            case "Maximize": NativeInterop.ShowWindow(hwnd, 3); break;
            case "Minimize": NativeInterop.ShowWindow(hwnd, 6); break;
            case "Restore": NativeInterop.ShowWindow(hwnd, 9); break;
            case "Close": NativeInterop.PostMessage(hwnd, NativeInterop.WM_CLOSE, IntPtr.Zero, IntPtr.Zero); break;
            default: throw new ArgumentException($"未知的窗口操作 {operation}。", nameof(operation));
        }
    }

    private static void SendSingleKey(ushort vk, bool down)
    {
        var input = new NativeInterop.INPUT();
        input.type = 1; // INPUT_KEYBOARD
        input.Anonymous.ki = new NativeInterop.KEYBDINPUT
        {
            wVk = vk,
            dwFlags = down ? 0u : 2u, // KEYEVENTF_KEYUP
        };
        SendInputRaw([input]);
    }

    private static NativeInterop.INPUT MakeUnicodeInput(char ch, bool down)
    {
        var input = new NativeInterop.INPUT();
        input.type = 1;
        input.Anonymous.ki = new NativeInterop.KEYBDINPUT
        {
            wScan = ch,
            dwFlags = 4u | (down ? 0u : 2u), // KEYEVENTF_UNICODE | KEYUP
        };
        return input;
    }

    private static NativeInterop.INPUT MakeMouseMove(uint x, uint y)
    {
        var input = new NativeInterop.INPUT();
        input.type = 1;
        input.Anonymous.mi = new NativeInterop.MOUSEINPUT
        {
            dx = (int)x,
            dy = (int)y,
            dwFlags = 0x8000 | 0x0001, // MOUSEEVENTF_ABSOLUTE | MOVE
        };
        return input;
    }

    private static NativeInterop.INPUT MakeMouseButtonInput(uint flags)
    {
        var input = new NativeInterop.INPUT();
        input.type = 1;
        input.Anonymous.mi = new NativeInterop.MOUSEINPUT { dwFlags = flags };
        return input;
    }

    private static void SendInputRaw(NativeInterop.INPUT[] inputs)
    {
        _ = NativeInterop.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeInterop.INPUT>());
    }
}

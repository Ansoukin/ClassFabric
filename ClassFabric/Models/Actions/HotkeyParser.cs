using System;
using System.Collections.Generic;

namespace ClassFabric.Models.Actions;

/// <summary>
/// 将 “Ctrl+S” 形式的组合键描述解析为 Win32 虚拟键码序列。
/// </summary>
public static class HotkeyParser
{
    // 字母 A-Z 与数字 0-9 的虚拟键码同 ASCII 大写一致，由单字符分支处理；其余常用键在此显式列出。
    static readonly Dictionary<string, ushort> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ctrl"] = 0x11,
        ["Alt"] = 0x12,
        ["Shift"] = 0x10,
        ["Win"] = 0x5B,
        ["Enter"] = 0x0D,
        ["Esc"] = 0x1B,
        ["Tab"] = 0x09,
        ["Space"] = 0x20,
        ["Backspace"] = 0x08,
        ["Delete"] = 0x2E,
        ["Home"] = 0x24,
        ["End"] = 0x23,
        ["PgUp"] = 0x21,
        ["PgDn"] = 0x22,
        ["Left"] = 0x25,
        ["Up"] = 0x26,
        ["Right"] = 0x27,
        ["Down"] = 0x28,
        ["F1"] = 0x70,
        ["F2"] = 0x71,
        ["F3"] = 0x72,
        ["F4"] = 0x73,
        ["F5"] = 0x74,
        ["F6"] = 0x75,
        ["F7"] = 0x76,
        ["F8"] = 0x77,
        ["F9"] = 0x78,
        ["F10"] = 0x79,
        ["F11"] = 0x7A,
        ["F12"] = 0x7B,
    };

    /// <summary>
    /// 按 “+” 分割并解析每个键名。任一键名无法识别时整体失败。
    /// </summary>
    public static bool TryParse(string keys, out ushort[] virtualKeys)
    {
        virtualKeys = [];
        if (string.IsNullOrWhiteSpace(keys)) return false;

        var tokens = keys.Split('+');
        var parsed = new List<ushort>(tokens.Length);
        foreach (var token in tokens)
        {
            if (!TryParseSingle(token.Trim(), out var virtualKey)) return false;
            parsed.Add(virtualKey);
        }

        virtualKeys = [.. parsed];
        return true;
    }

    static bool TryParseSingle(string token, out ushort virtualKey)
    {
        virtualKey = 0;
        if (token.Length == 1)
        {
            var upper = char.ToUpperInvariant(token[0]);
            if (upper is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                virtualKey = (ushort)upper;
                return true;
            }
        }
        return NamedKeys.TryGetValue(token, out virtualKey);
    }
}

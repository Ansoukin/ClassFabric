using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ClassIsland.Models.Actions.SystemTools;

/// <summary>
/// 「模拟组合键」行动设置。
/// </summary>
public class KeyCombinationSettings
{
    [JsonPropertyName("notifyOnExecute")]
    public bool NotifyOnExecute { get; set; } = false;

    [JsonPropertyName("keys")]
    public List<KeyCombinationKey> Keys { get; set; } = [];
}

/// <summary>
/// 组合键中的一个按键。
/// </summary>
public class KeyCombinationKey
{
    /// <summary>Win32 虚拟键码；null 表示该槽位尚未录入。</summary>
    [JsonPropertyName("keyCode")]
    public byte? KeyCode { get; set; }

    /// <summary>按键名，仅用于设置界面显示。</summary>
    [JsonPropertyName("keyName")]
    public string KeyName { get; set; } = string.Empty;
}

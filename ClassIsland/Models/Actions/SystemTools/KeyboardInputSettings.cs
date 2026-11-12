using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ClassIsland.Models.Actions.SystemTools;

/// <summary>
/// 「模拟键盘」行动设置。
/// </summary>
public class KeyboardInputSettings
{
    [JsonPropertyName("notifyOnExecute")]
    public bool NotifyOnExecute { get; set; } = false;

    /// <summary>
    /// 旧版录制数据格式："键码:按键名" 字符串列表。
    /// 老用户配置里可能仍是这份旧数据，反序列化后由行动与设置控件转换成 <see cref="Actions"/>。
    /// </summary>
    [JsonPropertyName("keys")] public List<string> Keys { get; set; } = [];

    /// <summary>
    /// 新版录制数据：带按下/释放/间隔的完整按键序列。
    /// </summary>
    [JsonPropertyName("actions")] public List<KeyboardAction> Actions { get; set; } = [];
}

/// <summary>
/// 一条录制的键盘操作。
/// </summary>
public class KeyboardAction
{
    public enum ActionType
    {
        Press,
        KeyDown,
        KeyUp
    }

    [JsonPropertyName("type")] public ActionType Type { get; set; } = ActionType.Press;

    /// <summary>Win32 虚拟键码。</summary>
    [JsonPropertyName("keyCode")] public byte KeyCode { get; set; }

    /// <summary>按键名，仅用于设置界面显示。</summary>
    [JsonPropertyName("keyName")] public string KeyName { get; set; } = string.Empty;

    /// <summary>与本条操作前一条之间的间隔毫秒数。</summary>
    [JsonPropertyName("interval")] public long Interval { get; set; }
}

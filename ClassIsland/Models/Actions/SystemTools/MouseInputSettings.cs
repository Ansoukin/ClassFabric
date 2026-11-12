using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ClassIsland.Models.Actions.SystemTools;

/// <summary>
/// 「模拟鼠标」行动设置。
/// </summary>
public class MouseInputSettings
{
    [JsonPropertyName("notifyOnExecute")]
    public bool NotifyOnExecute { get; set; } = false;

    /// <summary>录制的鼠标操作序列。</summary>
    [JsonPropertyName("actions")] public List<MouseAction> Actions { get; set; } = [];
}

/// <summary>
/// 一条录制的鼠标操作。坐标为屏幕绝对坐标。
/// </summary>
public class MouseAction
{
    public enum ActionType
    {
        LeftClick,
        RightClick,
        MiddleClick,
        Scroll,
        DragMove
    }

    [JsonPropertyName("type")] public ActionType Type { get; set; }

    [JsonPropertyName("x")] public int X { get; set; }

    [JsonPropertyName("y")] public int Y { get; set; }

    /// <summary>滚轮增量，正值向上。</summary>
    [JsonPropertyName("scrollDelta")] public int ScrollDelta { get; set; }

    /// <summary>与本条操作前一条之间的间隔毫秒数。</summary>
    [JsonPropertyName("interval")] public long Interval { get; set; }

    /// <summary>该拖动条目是否为拖动结束点。</summary>
    [JsonPropertyName("isDragEnd")] public bool IsDragEnd { get; set; }
}

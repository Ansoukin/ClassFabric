using System.Text.Json.Serialization;

namespace ClassIsland.Models.Actions.SystemTools;

/// <summary>
/// 「窗口操作」行动设置。
/// </summary>
public class WindowOperationSettings
{
    [JsonPropertyName("notifyOnExecute")]
    public bool NotifyOnExecute { get; set; } = false;

    /// <summary>
    /// 对前台窗口的操作。取值为「最大化」「最小化」「向下还原」「关闭窗口」之一。
    /// 存中文键名是为了老用户配置无缝迁移，不要改成枚举。
    /// </summary>
    [JsonPropertyName("operation")] public string Operation { get; set; } = "最大化";
}

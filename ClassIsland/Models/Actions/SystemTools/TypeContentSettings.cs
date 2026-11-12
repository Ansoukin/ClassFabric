using System.Text.Json.Serialization;

namespace ClassIsland.Models.Actions.SystemTools;

/// <summary>
/// 「键入内容」行动设置。
/// </summary>
public class TypeContentSettings
{
    [JsonPropertyName("notifyOnExecute")]
    public bool NotifyOnExecute { get; set; } = false;

    /// <summary>要键入到前台窗口的文本。</summary>
    [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
}

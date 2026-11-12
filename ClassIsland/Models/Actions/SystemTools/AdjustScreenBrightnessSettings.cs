using System.Text.Json.Serialization;

namespace ClassIsland.Models.Actions.SystemTools;

/// <summary>
/// 调整屏幕亮度行动设置。属性键名沿用社区插件原值。
/// </summary>
public class AdjustScreenBrightnessSettings
{
    [JsonPropertyName("notifyOnExecute")]
    public bool NotifyOnExecute { get; set; } = false;

    /// <summary>
    /// 目标亮度百分比，0 最暗、100 最亮。
    /// </summary>
    [JsonPropertyName("brightnessPercent")]
    public int BrightnessPercent { get; set; } = 50;
}

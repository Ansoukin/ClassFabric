using System.Text.Json.Serialization;

namespace ClassIsland.Models.Actions.SystemTools;

/// <summary>
/// 高级计时关机行动设置。属性键名沿用社区插件原值。
/// </summary>
public class AdvancedShutdownSettings
{
    /// <summary>
    /// 初始倒计时分钟数，执行时不足 1 分钟按 1 分钟处理。
    /// </summary>
    [JsonPropertyName("minutes")]
    public int Minutes { get; set; } = 2;
}

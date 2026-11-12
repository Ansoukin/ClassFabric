using System.Text.Json.Serialization;

namespace ClassIsland.Models.Actions.SystemTools;

/// <summary>
/// 计时关机行动设置。属性键名沿用社区插件原值。
/// </summary>
public class ShutdownSettings
{
    [JsonPropertyName("notifyOnExecute")]
    public bool NotifyOnExecute { get; set; } = false;

    /// <summary>
    /// 关机倒计时秒数。
    /// </summary>
    [JsonPropertyName("seconds")]
    public int Seconds { get; set; } = 60;

    /// <summary>
    /// 是否显示系统关机提示框。false 时执行完命令立刻模拟一次回车确认，避免弹窗挂在桌面上。
    /// </summary>
    [JsonPropertyName("showPrompt")]
    public bool ShowPrompt { get; set; } = true;
}

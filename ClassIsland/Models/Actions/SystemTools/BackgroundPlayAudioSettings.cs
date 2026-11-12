using System.Text.Json.Serialization;

namespace ClassIsland.Models.Actions.SystemTools;

public class BackgroundPlayAudioSettings
{
    // 插件行动本体没有用到这个开关（只在等待播放完成时才有意义），键名保留以便老配置无损迁移。
    [JsonPropertyName("notifyOnExecute")]
    public bool NotifyOnExecute { get; set; } = false;

    [JsonPropertyName("audioFilePath")]
    public string AudioFilePath { get; set; } = string.Empty;

    [JsonPropertyName("waitForPlaybackCompleted")]
    public bool WaitForPlaybackCompleted { get; set; }
}

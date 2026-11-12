using System.Text.Json.Serialization;

namespace ClassIsland.Models.Actions.SystemTools;

public class ThemeSettings
{
    [JsonPropertyName("notifyOnExecute")]
    public bool NotifyOnExecute { get; set; } = false;

    // 取值沿用插件的中文文案（浅色/深色），老配置里的旧值能直接对上。
    [JsonPropertyName("theme")] public string Theme { get; set; } = "浅色";
}

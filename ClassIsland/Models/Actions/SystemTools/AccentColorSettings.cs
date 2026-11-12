using System.Text.Json.Serialization;

namespace ClassIsland.Models.Actions.SystemTools;

public class AccentColorSettings
{
    [JsonPropertyName("colorHex")] public string ColorHex { get; set; } = "#FF0078D4";

    [JsonPropertyName("notifyOnExecute")] public bool NotifyOnExecute { get; set; } = false;
}

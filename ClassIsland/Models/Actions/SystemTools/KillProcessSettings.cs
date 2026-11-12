using System.Text.Json.Serialization;

namespace ClassIsland.Models.Actions.SystemTools;

public class KillProcessSettings
{
    [JsonPropertyName("notifyOnExecute")]
    public bool NotifyOnExecute { get; set; } = false;

    [JsonPropertyName("processName")] public string ProcessName { get; set; } = string.Empty;
}

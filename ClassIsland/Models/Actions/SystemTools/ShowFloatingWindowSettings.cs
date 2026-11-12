using System.Text.Json.Serialization;

namespace ClassIsland.Models.Actions.SystemTools;

public class ShowFloatingWindowSettings
{
    [JsonPropertyName("notifyOnExecute")]
    public bool NotifyOnExecute { get; set; } = false;

    [JsonPropertyName("showFloatingWindow")]
    public bool ShowFloatingWindow { get; set; } = true;
}

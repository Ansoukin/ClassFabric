using System.Text.Json.Serialization;

namespace ClassIsland.Models.Actions.SystemTools;

public class SetVolumeSettings
{
    [JsonPropertyName("notifyOnExecute")]
    public bool NotifyOnExecute { get; set; } = false;

    [JsonPropertyName("volumePercent")] public float VolumePercent { get; set; } = 50f;
}

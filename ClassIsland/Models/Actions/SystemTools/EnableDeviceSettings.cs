using System.Text.Json.Serialization;

namespace ClassIsland.Models.Actions.SystemTools;

public class EnableDeviceSettings
{
    [JsonPropertyName("notifyOnExecute")]
    public bool NotifyOnExecute { get; set; } = false;

    [JsonPropertyName("deviceId")] public string DeviceId { get; set; } = string.Empty;
}

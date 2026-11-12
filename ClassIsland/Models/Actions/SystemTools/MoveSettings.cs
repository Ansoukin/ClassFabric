using System.Text.Json.Serialization;

namespace ClassIsland.Models.Actions.SystemTools;

public class MoveSettings
{
    [JsonPropertyName("operationType")] public string OperationType { get; set; } = "文件";

    [JsonPropertyName("sourcePath")] public string SourcePath { get; set; } = string.Empty;

    [JsonPropertyName("destinationPath")] public string DestinationPath { get; set; } = string.Empty;
}

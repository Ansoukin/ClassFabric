using System.Text.Json.Serialization;

namespace ClassIsland.Models.Actions.SystemTools;

public class DeleteSettings
{
    [JsonPropertyName("operationType")] public string OperationType { get; set; } = "文件";

    [JsonPropertyName("targetPath")] public string TargetPath { get; set; } = string.Empty;
}

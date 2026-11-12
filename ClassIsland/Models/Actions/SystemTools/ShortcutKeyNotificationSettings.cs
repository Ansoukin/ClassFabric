using System.Text.Json.Serialization;

namespace ClassIsland.Models.Actions.SystemTools;

/// <summary>
/// 快捷键类 SystemTools 行动的通用设置：只有一个「执行后提醒」开关。
/// 显示切换、锁屏、取消关机计划等一键行动共用此模型，属性键名沿用社区插件原值，老配置可无缝迁移。
/// </summary>
public class ShortcutKeyNotificationSettings
{
    [JsonPropertyName("notifyOnExecute")]
    public bool NotifyOnExecute { get; set; } = false;
}

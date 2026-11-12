using System;
using System.Text.Json.Serialization;
using ClassIsland.Services.SystemTools;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.Models.Automation.Triggers;

/// <summary>
/// 悬浮窗触发器的配置。ButtonId 存的是 GUID 的 "N" 格式字符串：
/// 悬浮窗布局（配置方案 JSON）与规则集字典都用它当键，服务端 API 也按字符串处理，改回 Guid 类型会破坏存量配置。
/// </summary>
public partial class FloatingWindowTriggerSettings : ObservableObject
{
    [ObservableProperty]
    [JsonPropertyName("buttonId")]
    private string _buttonId = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    [JsonPropertyName("icon")]
    private string _icon = FloatingWindowIconProvider.DefaultIconExpression;

    [ObservableProperty]
    [JsonPropertyName("buttonName")]
    private string _buttonName = "触发按钮 1";

    [ObservableProperty]
    [JsonPropertyName("isVisible")]
    private bool _isVisible = true;

    // 兼容旧插件配置里的排序字段；布局实际由悬浮窗编辑页的方案文件管理，此值不再参与排序
    [ObservableProperty]
    [JsonPropertyName("position")]
    private int _position = -1;
}

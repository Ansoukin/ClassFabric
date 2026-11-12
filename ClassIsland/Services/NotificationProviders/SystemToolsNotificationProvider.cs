using ClassIsland.Core.Abstractions.Services.NotificationProviders;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Icons;

namespace ClassIsland.Services.NotificationProviders;

/// <summary>
/// SystemTools 行动执行通知提供方。行动里勾选「执行后通知」时经此显示遮罩提醒。
/// Provider 与渠道 GUID 沿用社区插件 SystemTools 原值，老用户的通知渠道设置可以无缝延续。
/// </summary>
[NotificationProviderInfo("7E9A3D5C-1B8F-4E2A-9C6D-0F5E8B1A4D7C", "SystemTools 通知",
    FluentIcons.IotAlertRegular, "来自系统工具行动的提醒。")]
[NotificationChannelInfo("6F8C2B4A-9D1E-5F3B-8A7C-1E4D9F6B3A8C", "SystemTools",
    FluentIcons.IotAlertRegular, "系统工具通用通知渠道")]
public class SystemToolsNotificationProvider : NotificationProviderBase
{
}

using System;
using System.Linq;
using System.Threading.Tasks;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Icons;
using ClassIsland.Core.Models.Notification;
using ClassIsland.Models.Actions.SystemTools;
using ClassIsland.Platforms.Abstraction;
using ClassIsland.Services.NotificationProviders;
using ClassIsland.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services.Automation.Actions.SystemTools;

// 插件里类名就叫 SetVolumeAction（文件名是 SetVolume.cs），保留原类名避免检索时对不上。
[AutomationCategory("实用工具")]
[ActionInfo("SystemTools.SetVolume", "设置系统音量", FluentIcons.SpeakerEditRegular, false)]
public class SetVolumeAction(ILogger<SetVolumeAction> logger) : ActionBase<SetVolumeSettings>
{
    private readonly ILogger<SetVolumeAction> _logger = logger;

    static SystemToolsNotificationProvider? NotificationProvider =>
        IAppHost.Host?.Services.GetServices<IHostedService>()
            .OfType<SystemToolsNotificationProvider>().FirstOrDefault();

    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        // WASAPI COM Interop 一整段（MMDeviceEnumerator/IAudioEndpointVolume）下沉到平台服务；
        // mute 传 null 表示不变更静音状态，只改音量。
        if (!PlatformServices.SystemEnvironmentService.SetSystemVolume(Settings.VolumePercent, null))
        {
            _logger.LogError("设置音量失败：目标 {Volume}%", Settings.VolumePercent);
            throw new InvalidOperationException("设置系统音量失败，当前平台可能不支持。");
        }

        _logger.LogInformation("音量设置为 {Volume}%", Settings.VolumePercent);

        if (Settings.NotifyOnExecute)
        {
            NotificationProvider?.ShowNotification(new NotificationRequest
            {
                MaskContent = NotificationContent.CreateTwoIconsMask("已自动调整系统音量", FluentIcons.IotAlertRegular, "")
            });
        }
    }
}

using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using ClassFabric.Core.Abstractions.Automation;
using ClassFabric.Core.Attributes;
using ClassFabric.Core.Models.Notification;
using ClassFabric.Models.Actions;
using ClassFabric.Services.NotificationProviders;
using ClassFabric.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClassFabric.Services.Automation.Actions;

[ActionInfo("classfabric.action.showNotification", "发送通知", "\uEA8F")]
public class ShowNotificationAction : ActionBase<ShowNotificationActionSettings>
{
    static ActionNotificationProvider ActionNotificationProvider { get; } =
        IAppHost.Host.Services.GetServices<IHostedService>().OfType<ActionNotificationProvider>().First();

    protected override async Task OnInvoke()
    {
        await base.OnInvoke();
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var request = new NotificationRequest
            {
                MaskContent = NotificationContent.CreateTwoIconsMask(Settings.Title),
                OverlayContent = string.IsNullOrEmpty(Settings.Content)
                    ? null
                    : NotificationContent.CreateSimpleTextContent(Settings.Content)
            };
            await ActionNotificationProvider.ShowNotificationAsync(request);
        });
    }
}

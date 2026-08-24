using System.Threading.Tasks;
using ClassFabric.Core.Abstractions.Automation;
using ClassFabric.Core.Abstractions.Services;
using ClassFabric.Core.Attributes;
using ClassFabric.Models.Actions;
using ClassFabric.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace ClassFabric.Services.Automation.Actions;

[ActionInfo("classfabric.action.clearNotifications", "清除全部提醒", "\uED94")]
public class ClearNotificationsAction : ActionBase<ClearNotificationsActionSettings>
{
    protected override async Task OnInvoke()
    {
        await base.OnInvoke();
        IAppHost.Host.Services.GetRequiredService<INotificationHostService>()
            .CancelAllNotifications();
    }
}

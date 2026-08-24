using System.Threading.Tasks;
using ClassFabric.Core.Abstractions.Automation;
using ClassFabric.Core.Attributes;
using ClassFabric.Models.Actions;
using ClassFabric.Services.Automation.PlatformTools;
using ClassFabric.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace ClassFabric.Services.Automation.Actions;

[ActionInfo("classfabric.action.immediateShutdown", "立即关机", "\uE7BA")]
public class ImmediateShutdownAction : ActionBase<ImmediateShutdownActionSettings>
{
    private PowerService PowerService =>
        IAppHost.Host.Services.GetRequiredService<PowerService>();

    protected override async Task OnInvoke()
    {
        await base.OnInvoke();
        PowerService.PowerOff();
    }
}

using System.Threading.Tasks;
using ClassFabric.Core.Abstractions.Automation;
using ClassFabric.Core.Attributes;
using ClassFabric.Models.Actions;
using ClassFabric.Services.Automation.PlatformTools;
using ClassFabric.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace ClassFabric.Services.Automation.Actions;

[ActionInfo("classfabric.action.restartAsAdmin", "以管理员身份重启", "\uE7EF")]
public class RestartAsAdminAction : ActionBase<RestartAsAdminActionSettings>
{
    protected override async Task OnInvoke()
    {
        await base.OnInvoke();
        IAppHost.Host.Services.GetRequiredService<ElevationService>().RestartSelfAsAdmin();
    }
}

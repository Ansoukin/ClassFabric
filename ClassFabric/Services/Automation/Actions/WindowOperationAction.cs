using System.Threading.Tasks;
using ClassFabric.Core.Abstractions.Automation;
using ClassFabric.Core.Attributes;
using ClassFabric.Models.Actions;
using ClassFabric.Services.Automation.PlatformTools;
using ClassFabric.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace ClassFabric.Services.Automation.Actions;

[ActionInfo("classfabric.action.windowOperation", "窗口操作", "\uE8A7")]
public class WindowOperationAction : ActionBase<WindowOperationActionSettings>
{
    private InputSimulationService InputSimulationService =>
        IAppHost.Host.Services.GetRequiredService<InputSimulationService>();

    protected override async Task OnInvoke()
    {
        await base.OnInvoke();
        InputSimulationService.OperateForegroundWindow(Settings.Operation);
    }
}

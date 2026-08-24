using System.Threading.Tasks;
using ClassFabric.Core.Abstractions.Automation;
using ClassFabric.Core.Attributes;
using ClassFabric.Models.Actions;
using ClassFabric.Services.Automation.PlatformTools;
using ClassFabric.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace ClassFabric.Services.Automation.Actions;

[ActionInfo("classfabric.action.simulateMouse", "模拟鼠标操作", "\uE7C9")]
public class SimulateMouseAction : ActionBase<SimulateMouseActionSettings>
{
    private InputSimulationService InputSimulationService =>
        IAppHost.Host.Services.GetRequiredService<InputSimulationService>();

    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        if (Settings.Steps.Count == 0) return;
        foreach (var step in Settings.Steps)
        {
            switch (step.Type)
            {
                case 1:
                    InputSimulationService.SendMouseButton(step.X, step.Y, true);
                    break;
                case 2:
                    InputSimulationService.MoveMouseTo(step.X, step.Y);
                    break;
                case 3:
                    InputSimulationService.SendMouseWheel(step.Delta);
                    break;
                default:
                    InputSimulationService.SendMouseButton(step.X, step.Y, false);
                    break;
            }
        }
    }
}

using System.Threading.Tasks;
using ClassFabric.Core.Abstractions.Automation;
using ClassFabric.Core.Attributes;
using ClassFabric.Models.Actions;
using ClassFabric.Services.Automation.PlatformTools;
using ClassFabric.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace ClassFabric.Services.Automation.Actions;

[ActionInfo("classfabric.action.simulateKeyCombination", "模拟组合键", "\uE765")]
public class SimulateKeyCombinationAction : ActionBase<SimulateKeyCombinationActionSettings>
{
    private InputSimulationService InputSimulationService =>
        IAppHost.Host.Services.GetRequiredService<InputSimulationService>();

    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        // 键串写错时静默跳过，避免因单个行动中断整个行动组。
        if (!HotkeyParser.TryParse(Settings.Keys, out var virtualKeys))
            return;
        InputSimulationService.SendKeySequence(virtualKeys);
    }
}

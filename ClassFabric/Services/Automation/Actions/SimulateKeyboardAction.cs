using System;
using System.Threading.Tasks;
using ClassFabric.Core.Abstractions.Automation;
using ClassFabric.Core.Attributes;
using ClassFabric.Models.Actions;
using ClassFabric.Services.Automation.PlatformTools;
using ClassFabric.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace ClassFabric.Services.Automation.Actions;

[ActionInfo("classfabric.action.simulateKeyboard", "模拟键盘序列", "\uE751")]
public class SimulateKeyboardAction : ActionBase<SimulateKeyboardActionSettings>
{
    private InputSimulationService InputSimulationService =>
        IAppHost.Host.Services.GetRequiredService<InputSimulationService>();

    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        if (Settings.Steps.Count == 0) return;

        // 每步独立按下再抬起；键名解析失败时跳过该步，不中断后续步骤。
        foreach (var step in Settings.Steps)
        {
            if (!HotkeyParser.TryParse(step.Key, out var virtualKeys)) continue;
            foreach (var key in virtualKeys)
                InputSimulationService.SendKeySequence([key]);
            await Task.Delay(Math.Max(0, step.IntervalMs));
        }
    }
}

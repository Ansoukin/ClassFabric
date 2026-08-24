using System.Collections.Generic;
using System.Linq;
using ClassFabric.Core.Abstractions.Controls;
using ClassFabric.Models.Actions;
using ClassFabric.Services.Automation.PlatformTools;

namespace ClassFabric.Controls.ActionSettingsControls;

public partial class SimulateHotkeyActionSettingsControl : ActionSettingsControlBase<SimulateHotkeyActionSettings>
{
    public IReadOnlyList<string> HotkeyPresetNames { get; } =
        InputSimulationService.HotkeyPresetNames.ToList();

    public SimulateHotkeyActionSettingsControl() => InitializeComponent();
}

using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.Models.Actions;

public class SimulateHotkeyActionSettings : ObservableRecipient
{
    /// <summary>
    /// 预设快捷键名，取值见 InputSimulationService.HotkeyPresetNames。
    /// </summary>
    string _preset = "AltF4";
    public string Preset
    {
        get => _preset;
        set
        {
            if (value == _preset) return;
            _preset = value;
            OnPropertyChanged();
        }
    }
}

using ClassFabric.Core.Attributes;
using ClassFabric.Shared.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.Models.Automation.Triggers;

public partial class PreTimePointTriggerSettings : ObservableObject
{
    [ObservableProperty] private TimeState _targetState = TimeState.OnClass;

    [ObservableProperty] private double _timeSeconds = 60;
}
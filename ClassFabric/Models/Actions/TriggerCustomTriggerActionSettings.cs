using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.Models.Actions;

public class TriggerCustomTriggerActionSettings : ObservableRecipient
{
    private Guid _targetTriggerId = Guid.Empty;

    public Guid TargetTriggerId
    {
        get => _targetTriggerId;
        set
        {
            if (value == _targetTriggerId) return;
            _targetTriggerId = value;
            OnPropertyChanged();
        }
    }
}

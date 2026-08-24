using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.Models.Actions;

public class LoadTempClassPlanActionSettings : ObservableRecipient
{
    Guid _classPlanId = Guid.Empty;
    public Guid ClassPlanId
    {
        get => _classPlanId;
        set
        {
            if (value == _classPlanId) return;
            _classPlanId = value;
            OnPropertyChanged();
        }
    }
}

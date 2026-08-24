using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.Models.Rules;

public class UsingClassPlanRuleSettings : ObservableRecipient
{
    private Guid _classPlanId = Guid.Empty;

    /// <summary>
    /// 要匹配的课表 GUID。
    /// </summary>
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

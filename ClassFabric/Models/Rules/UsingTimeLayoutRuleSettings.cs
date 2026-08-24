using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.Models.Rules;

public class UsingTimeLayoutRuleSettings : ObservableRecipient
{
    private Guid _timeLayoutId = Guid.Empty;

    /// <summary>
    /// 要匹配的时间表 GUID。
    /// </summary>
    public Guid TimeLayoutId
    {
        get => _timeLayoutId;
        set
        {
            if (value == _timeLayoutId) return;
            _timeLayoutId = value;
            OnPropertyChanged();
        }
    }
}

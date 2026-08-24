using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.Models.Rules;

public class InTimePeriodRuleSettings : ObservableRecipient
{
    private string _start = "08:00";
    private string _end = "17:00";

    /// <summary>
    /// 起始时间（HH:mm）。
    /// </summary>
    public string Start
    {
        get => _start;
        set
        {
            if (value == _start) return;
            _start = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 结束时间（HH:mm）。早于起始时间时表示跨零点区间。
    /// </summary>
    public string End
    {
        get => _end;
        set
        {
            if (value == _end) return;
            _end = value;
            OnPropertyChanged();
        }
    }
}

using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.Models.Automation.Triggers;

public class LongIdleTriggerSettings : ObservableRecipient
{
    private int _idleMinutes = 5;

    public int IdleMinutes
    {
        get => _idleMinutes;
        set
        {
            // 低于 1 分钟的取值没有意义，统一按 1 分钟处理
            var normalized = Math.Max(1, value);
            if (_idleMinutes == normalized) return;
            _idleMinutes = normalized;
            OnPropertyChanged();
        }
    }
}

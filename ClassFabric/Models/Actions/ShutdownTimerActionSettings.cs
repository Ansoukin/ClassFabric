using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.Models.Actions;

public class ShutdownTimerActionSettings : ObservableRecipient
{
    int _seconds = 60;
    public int Seconds
    {
        get => _seconds;
        set
        {
            if (value == _seconds) return;
            _seconds = value;
            OnPropertyChanged();
        }
    }

    bool _showNotification = true;
    public bool ShowNotification
    {
        get => _showNotification;
        set
        {
            if (value == _showNotification) return;
            _showNotification = value;
            OnPropertyChanged();
        }
    }
}

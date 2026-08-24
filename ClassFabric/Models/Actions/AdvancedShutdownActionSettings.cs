using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.Models.Actions;

public class AdvancedShutdownActionSettings : ObservableRecipient
{
    int _seconds = 300;
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
}

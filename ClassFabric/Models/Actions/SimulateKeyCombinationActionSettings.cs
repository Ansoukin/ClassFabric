using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.Models.Actions;

public class SimulateKeyCombinationActionSettings : ObservableRecipient
{
    string _keys = "Ctrl+S";
    public string Keys
    {
        get => _keys;
        set
        {
            if (value == _keys) return;
            _keys = value;
            OnPropertyChanged();
        }
    }
}

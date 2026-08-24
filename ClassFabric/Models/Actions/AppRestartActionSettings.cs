using CommunityToolkit.Mvvm.ComponentModel;
namespace ClassFabric.Models.Actions;

public class AppRestartActionSettings : ObservableRecipient
{
    bool _value = false;
    public bool Value
    {
        get => _value;
        set
        {
            if (value == _value) return;
            _value = value;
            OnPropertyChanged();
        }
    }
}
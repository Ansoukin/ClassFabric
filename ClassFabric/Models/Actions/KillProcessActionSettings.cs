using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.Models.Actions;

public class KillProcessActionSettings : ObservableRecipient
{
    string _processName = "";
    public string ProcessName
    {
        get => _processName;
        set
        {
            if (value == _processName) return;
            _processName = value;
            OnPropertyChanged();
        }
    }
}

using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.Models.Actions;

public class CopyPathActionSettings : ObservableRecipient
{
    string _source = "";
    public string Source
    {
        get => _source;
        set
        {
            if (value == _source) return;
            _source = value;
            OnPropertyChanged();
        }
    }

    string _target = "";
    public string Target
    {
        get => _target;
        set
        {
            if (value == _target) return;
            _target = value;
            OnPropertyChanged();
        }
    }

    bool _overwrite;
    public bool Overwrite
    {
        get => _overwrite;
        set
        {
            if (value == _overwrite) return;
            _overwrite = value;
            OnPropertyChanged();
        }
    }
}

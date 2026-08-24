using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.Models.Actions;

public class DeletePathActionSettings : ObservableRecipient
{
    string _path = "";
    public string Path
    {
        get => _path;
        set
        {
            if (value == _path) return;
            _path = value;
            OnPropertyChanged();
        }
    }

    bool _recursive = true;
    public bool Recursive
    {
        get => _recursive;
        set
        {
            if (value == _recursive) return;
            _recursive = value;
            OnPropertyChanged();
        }
    }
}

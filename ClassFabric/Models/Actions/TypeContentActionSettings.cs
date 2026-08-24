using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.Models.Actions;

public class TypeContentActionSettings : ObservableRecipient
{
    string _content = "";
    public string Content
    {
        get => _content;
        set
        {
            if (value == _content) return;
            _content = value;
            OnPropertyChanged();
        }
    }

    bool _isFromClipboard;
    public bool IsFromClipboard
    {
        get => _isFromClipboard;
        set
        {
            if (value == _isFromClipboard) return;
            _isFromClipboard = value;
            OnPropertyChanged();
        }
    }
}

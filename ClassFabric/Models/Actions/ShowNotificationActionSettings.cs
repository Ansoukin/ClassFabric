using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.Models.Actions;

public class ShowNotificationActionSettings : ObservableRecipient
{
    string _title = "";
    public string Title
    {
        get => _title;
        set
        {
            if (value == _title) return;
            _title = value;
            OnPropertyChanged();
        }
    }

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
}

using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.Models.Actions;

public class FlowConfirmationActionSettings : ObservableRecipient
{
    private string _message = string.Empty;
    private bool _allowDelay = true;

    public string Message
    {
        get => _message;
        set
        {
            if (value == _message) return;
            _message = value;
            OnPropertyChanged();
        }
    }

    public bool AllowDelay
    {
        get => _allowDelay;
        set
        {
            if (value == _allowDelay) return;
            _allowDelay = value;
            OnPropertyChanged();
        }
    }
}

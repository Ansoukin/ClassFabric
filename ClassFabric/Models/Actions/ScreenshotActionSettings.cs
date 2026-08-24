using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.Models.Actions;

public class ScreenshotActionSettings : ObservableRecipient
{
    string _saveDirectory = "";
    public string SaveDirectory
    {
        get => _saveDirectory;
        set
        {
            if (value == _saveDirectory) return;
            _saveDirectory = value;
            OnPropertyChanged();
        }
    }
}

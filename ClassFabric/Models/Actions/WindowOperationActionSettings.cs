using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.Models.Actions;

/// <summary>
/// <see cref="Operation"/> 取值须为 InputSimulationService.OperateForegroundWindow 支持的操作名。
/// </summary>
public class WindowOperationActionSettings : ObservableRecipient
{
    string _operation = "Maximize";
    public string Operation
    {
        get => _operation;
        set
        {
            if (value == _operation) return;
            _operation = value;
            OnPropertyChanged();
        }
    }
}

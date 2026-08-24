using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.Models.Rules;

public class ProcessRunningRuleSettings : ObservableRecipient
{
    private string _processName = "";

    /// <summary>
    /// 要检测的进程名，不含扩展名。
    /// </summary>
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

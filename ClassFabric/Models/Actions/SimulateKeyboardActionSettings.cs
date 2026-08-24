using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.Models.Actions;

public class SimulateKeyboardActionSettings : ObservableRecipient
{
    ObservableCollection<KeyStep> _steps = [];
    public ObservableCollection<KeyStep> Steps
    {
        get => _steps;
        set
        {
            if (ReferenceEquals(value, _steps)) return;
            _steps = value;
            OnPropertyChanged();
        }
    }
}

/// <summary>
/// 键盘序列中的一步：按下再抬起 <see cref="Key"/>，等待 <see cref="IntervalMs"/> 后进入下一步。
/// 首版设置界面只做展示与就地编辑，新增或删除步骤需在配置文件中手工完成。
/// </summary>
public class KeyStep : ObservableRecipient
{
    string _key = "A";
    public string Key
    {
        get => _key;
        set
        {
            if (value == _key) return;
            _key = value;
            OnPropertyChanged();
        }
    }

    int _intervalMs = 100;
    public int IntervalMs
    {
        get => _intervalMs;
        set
        {
            if (value == _intervalMs) return;
            _intervalMs = value;
            OnPropertyChanged();
        }
    }
}

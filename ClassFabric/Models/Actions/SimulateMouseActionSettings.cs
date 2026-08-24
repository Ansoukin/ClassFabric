using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.Models.Actions;

public class SimulateMouseActionSettings : ObservableRecipient
{
    ObservableCollection<MouseStep> _steps = [];
    public ObservableCollection<MouseStep> Steps
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
/// 鼠标操作序列中的一步。<see cref="Type"/> 取值：0 左击、1 右击、2 移动到 (X, Y)、3 滚动 <see cref="Delta"/>。
/// 首版设置界面只做展示与就地编辑，新增或删除步骤需在配置文件中手工完成。
/// </summary>
public class MouseStep : ObservableRecipient
{
    int _type;
    public int Type
    {
        get => _type;
        set
        {
            if (value == _type) return;
            _type = value;
            OnPropertyChanged();
        }
    }

    int _x;
    public int X
    {
        get => _x;
        set
        {
            if (value == _x) return;
            _x = value;
            OnPropertyChanged();
        }
    }

    int _y;
    public int Y
    {
        get => _y;
        set
        {
            if (value == _y) return;
            _y = value;
            OnPropertyChanged();
        }
    }

    int _delta = 120;
    public int Delta
    {
        get => _delta;
        set
        {
            if (value == _delta) return;
            _delta = value;
            OnPropertyChanged();
        }
    }
}

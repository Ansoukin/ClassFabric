using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.Models.Actions;

public class ToggleWorkflowActionSettings : ObservableRecipient
{
    public const string ModeEnable = "Enable";
    public const string ModeDisable = "Disable";
    public const string ModeToggle = "Toggle";

    private Guid _targetProfileId = Guid.Empty;
    private string _mode = ModeToggle;

    public Guid TargetProfileId
    {
        get => _targetProfileId;
        set
        {
            if (value == _targetProfileId) return;
            _targetProfileId = value;
            OnPropertyChanged();
        }
    }

    public string Mode
    {
        get => _mode;
        set
        {
            if (value == _mode) return;
            _mode = value;
            OnPropertyChanged();
        }
    }
}

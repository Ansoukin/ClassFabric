using System.Collections.Generic;

using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.Models.AllContributors;

public class AllContributorsRc : ObservableRecipient
{
    private List<Contributor> _contributors = new();

    public List<Contributor> Contributors
    {
        get => _contributors;
        set
        {
            if (Equals(value, _contributors)) return;
            _contributors = value;
            OnPropertyChanged();
        }
    }
}
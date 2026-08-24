using System.Collections.ObjectModel;
using ClassFabric.Core.Abstractions.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassFabric.Models.ComponentSettings;

public partial class GroupComponentSettings : ObservableObject, IComponentContainerSettings
{
    [ObservableProperty]
    private ObservableCollection<Core.Models.Components.ComponentSettings> _children = [];
}
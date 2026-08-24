using System.Collections.Generic;
using Avalonia.Interactivity;
using ClassFabric.Core.Models.Components;

namespace ClassFabric.Controls.EditMode;

public class RequestAddComponentEventArgs(RoutedEvent e) : RoutedEventArgs(e)
{
    public required IList<ComponentSettings> ComponentList { get; init; }
}
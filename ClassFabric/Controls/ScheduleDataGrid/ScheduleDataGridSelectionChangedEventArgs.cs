using System;
using Avalonia.Interactivity;
using ClassFabric.Shared.Models.Profile;

namespace ClassFabric.Controls.ScheduleDataGrid;

public class ScheduleDataGridSelectionChangedEventArgs(RoutedEvent e) : RoutedEventArgs(e)
{
    public required ClassInfo ClassInfo { get; init; }
    
    public required DateTime Date { get; init; }
}
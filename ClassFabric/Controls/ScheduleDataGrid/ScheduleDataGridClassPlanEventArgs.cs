using System;
using Avalonia.Interactivity;
using ClassFabric.Shared.Models.Profile;

namespace ClassFabric.Controls.ScheduleDataGrid;

public class ScheduleDataGridClassPlanEventArgs(RoutedEvent e) : RoutedEventArgs(e)
{
    public required ClassPlan ClassPlan { get; init; }
    
    public required DateTime Date { get; set; }
}
using System;

namespace ClassFabric.Controls.ScheduleDataGrid;

public class CreateClassPlanEventArgs : EventArgs
{
    public required DateTime Date { get; init; }
}
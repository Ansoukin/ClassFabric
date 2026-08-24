using Avalonia.Interactivity;
using ClassFabric.Shared.Models.Profile;

namespace ClassFabric.Controls.TimeLine;

public class TimeLineInsertTimePointEventArgs(RoutedEvent e) : RoutedEventArgs(e)
{
    public required int Kind { get; init; }
    
    public required InsertLocation Location { get; init; }
    
    public required TimeLayoutItem TimePoint { get; init; }
    
    public enum InsertLocation
    {
        Before,
        After,
        Inside
    }
}
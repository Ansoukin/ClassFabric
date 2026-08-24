using System.Windows;
using Avalonia.Interactivity;
using ClassFabric.Controls;
using ClassFabric.Controls.TimeLine;
using ClassFabric.Shared.Models.Profile;

namespace ClassFabric.Models.EventArgs;

public class SeparatorLikeTimePointMovedEventArgs(TimeLayoutItem item) : RoutedEventArgs(TimeLineListItemSeparatorAdornerControl.SeparatorLikeTimePointMovedEvent)
{
    public TimeLayoutItem Item { get; } = item;
}
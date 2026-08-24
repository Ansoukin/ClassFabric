using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace ClassFabric.Core.Controls;

public class HighlightBox : ContentControl
{
    public static readonly StyledProperty<bool>? IsHighlightProperty = AvaloniaProperty.Register<HighlightBox, bool>(
        nameof(IsHighlight));

    public bool IsHighlight
    {
        get => GetValue(IsHighlightProperty);
        set => SetValue(IsHighlightProperty, value);
    }
}
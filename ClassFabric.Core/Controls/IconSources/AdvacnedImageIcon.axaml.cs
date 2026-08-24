using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using ClassFabric.Core.Abstractions.Controls.IconSources;

namespace ClassFabric.Core.Controls.IconSources;

/// <summary>
/// 
/// </summary>
public class AdvancedImageIcon : TemplatedIconElement
{
    /// <summary>
    /// 
    /// </summary>
    public static readonly StyledProperty<AdvancedImageIconSource> IconSourceProperty = AvaloniaProperty.Register<AdvancedImageIcon, AdvancedImageIconSource>(
        nameof(IconSource));

    /// <summary>
    /// 
    /// </summary>
    public AdvancedImageIconSource IconSource
    {
        get => GetValue(IconSourceProperty);
        set => SetValue(IconSourceProperty, value);
    }
}
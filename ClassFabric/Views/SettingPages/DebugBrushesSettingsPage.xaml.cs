#if false
using ClassFabric.Core.Abstractions.Controls;
using ClassFabric.Core.Attributes;
using ClassFabric.Core.Enums.SettingsWindow;

namespace ClassFabric.Views.SettingPages;

/// <summary>
/// DebugBrushesSettingsPage.xaml 的交互逻辑
/// </summary>
[SettingsPageInfo("debug_brushes", "笔刷", MaterialIconKind.BrushOutline, MaterialIconKind.Brush, SettingsPageCategory.Debug)]
public partial class DebugBrushesSettingsPage : SettingsPageBase
{
    public DebugBrushesSettingsPage()
    {
        InitializeComponent();
        DataContext = this;
    }
}
#endif

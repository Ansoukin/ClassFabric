using System.Collections.ObjectModel;
using ClassFabric.Core.Models.Plugin;

namespace ClassFabric.Core.Models.XamlTheme;

/// <summary>
/// 主题仓库索引
/// </summary>
public class ThemeIndex
{
    /// <summary>
    /// 主题仓库包含的主题列表
    /// </summary>
    public ObservableCollection<ThemeIndexItem> Themes { get; set; } = [];
}
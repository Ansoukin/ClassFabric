namespace ClassIsland.Core.Models.Plugin;

/// <summary>
/// 内置插件的宿主能力委托集合。插件与主工程不允许互相引用，主工程侧需要交给插件的能力
/// （如设置读取、主题取色）以委托形式经 <see cref="PluginBase.BuiltInConfig"/> 传入，
/// 插件只在运行时调用，不感知主工程类型。
/// </summary>
public class BuiltInPluginConfig
{
    /// <summary>
    /// 获取当前主题主色（Settings.PrimaryColor）。特效需要主题色兜底时使用。
    /// </summary>
    public Func<Avalonia.Media.Color>? FallbackThemeColor { get; set; }
}

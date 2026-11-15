using System;

namespace ClassIsland.UltraCodeShared;

/// <summary>
/// 像素场动画的调参来源。
/// </summary>
/// <remarks>
/// 这套渲染代码被三个插件按源码共享（UltraCode / 值日提醒 / 课后自动复原）。
/// 参数的<b>唯一权威</b>是 UltraCode 插件的 <c>options.json</c>：
/// UltraCode 自己注入可写的设置单例，另外两个插件注入
/// <see cref="JsonTuningSource"/> 去读同一个文件。
/// 这样用户在 UltraCode 设置页拖滑块，三处动画一起变。
/// <para/>
/// 之所以不直接跨插件引用 UltraCode 的 dll：每个插件由宿主用独立的
/// PluginLoadContext 加载，引用会得到<b>不同的程序集身份</b>，
/// 而调色板和设置都是进程级静态，共享必然打架。
/// </remarks>
public interface IUltraCodeTuning
{
    /// <summary>动画整体不透明度。不跟随主界面的「背景不透明度」。</summary>
    double AnimationOpacity { get; }

    /// <summary>像素亮度倍率。</summary>
    double PixelIntensity { get; }

    /// <summary>饱和度。到 2.0 即为该色相的纯色。</summary>
    double Saturation { get; }

    /// <summary>对比度，决定亮度带的宽度。</summary>
    double Contrast { get; }

    /// <summary>亮度偏移，决定亮度带的中心。</summary>
    double Brightness { get; }

    /// <summary>像素格边长（控件坐标）。小于等于 0 时用控件自己的 CellSize。</summary>
    double CellSize { get; }

    /// <summary>流动速度倍率。</summary>
    double FlowSpeed { get; }

    /// <summary>倒计时是否用彩带长度递减表达剩余时间。</summary>
    bool CountdownDrain { get; }
}

/// <summary>出厂默认值。UltraCode 插件没装、或配置文件读不到时用这一套。</summary>
public sealed class DefaultUltraCodeTuning : IUltraCodeTuning
{
    public double AnimationOpacity => 0.92;
    public double PixelIntensity => 1.95;
    public double Saturation => 3.0;
    public double Contrast => 1.45;
    public double Brightness => 0.07;
    public double CellSize => 6;
    public double FlowSpeed => 1.0;
    public bool CountdownDrain => true;
}

/// <summary>当前生效的调参来源。插件启动时设一次。</summary>
public static class UltraCodeTuning
{
    private static IUltraCodeTuning _current = new DefaultUltraCodeTuning();

    /// <summary>调参来源。没设过就是 <see cref="DefaultUltraCodeTuning"/>。</summary>
    public static IUltraCodeTuning Current
    {
        get => _current;
        set
        {
            _current = value ?? new DefaultUltraCodeTuning();
            UltraCodePalette.Invalidate();
        }
    }
}

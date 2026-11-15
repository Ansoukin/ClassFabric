using System;
using System.IO;
using System.Text.Json;
using ClassIsland.UltraCodeShared;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.UltraCode;

/// <summary>
/// UltraCode 动画的可调参数。全局单例，设置页直接绑这上面。
/// </summary>
/// <remarks>
/// 实现了 <see cref="ObservableObject"/>，所以主题 XAML 里可以用
/// <c>{Binding Source={x:Static uc:UltraCodeOptions.Current}, Path=...}</c> 直接绑，
/// 改完立刻生效，不用重启也不用重载主题。
/// </remarks>
public partial class UltraCodeOptions : ObservableObject, IUltraCodeTuning
{
    /// <summary>全局实例。</summary>
    public static UltraCodeOptions Current { get; private set; } = new();

    private static string? _configPath;

    #region 动画强度

    /// <summary>
    /// 动画层整体不透明度。
    /// </summary>
    /// <remarks>
    /// 刻意**不跟随**主界面的「背景不透明度」。原来跟随的时候，
    /// 用户把主界面调透一点，提醒动画就跟着一起淡到几乎看不见了——
    /// 但提醒本来就是要被看清的，这两件事不该绑在一起。
    /// </remarks>
    [ObservableProperty] private double _animationOpacity = 0.92;

    /// <summary>闪烁像素层的亮度倍率。底色渐变不受影响。</summary>
    [ObservableProperty] private double _pixelIntensity = 1.95;

    #endregion

    #region 取色微调（在系统自动取色的基础上）

    /// <summary>
    /// 往「主题色的纯色」靠多少。0 = 保留参考色阶原本的灰调，2.0 及以上 = 完全的纯色。
    /// </summary>
    /// <remarks>
    /// 默认 3.0，也就是直接用主题色最鲜艳的那一档。
    /// 老算法把饱和度乘上主题色自身的饱和度、又保留了参考色阶接近纯白的亮度，
    /// 两道天花板叠在一起，滑块拉到头也还是发灰；现在这个值是纯色插值的权重。
    /// </remarks>
    [ObservableProperty] private double _saturation = 3.0;

    /// <summary>对比度。以中灰为轴拉开明暗，1.0 为不变。</summary>
    [ObservableProperty] private double _contrast = 1.45;

    /// <summary>亮度偏移。正数整体提亮，负数压暗。</summary>
    [ObservableProperty] private double _brightness = 0.07;

    #endregion

    #region 像素场

    /// <summary>单个像素格边长（逻辑像素）。调大 = 颗粒更粗。</summary>
    [ObservableProperty] private double _cellSize = 6;

    /// <summary>流动速度倍率。1.0 是原速，越大流得越快。</summary>
    [ObservableProperty] private double _flowSpeed = 1.0;

    /// <summary>倒计时是否用彩带长度递减来表达剩余时间。</summary>
    [ObservableProperty] private bool _countdownDrain = true;

    #endregion

    /// <summary>任何影响配色的参数变化时触发，用于让调色板重算。</summary>
    public static event EventHandler? PaletteInvalidated;

    partial void OnSaturationChanged(double value) => InvalidatePalette();
    partial void OnContrastChanged(double value) => InvalidatePalette();
    partial void OnBrightnessChanged(double value) => InvalidatePalette();

    private void InvalidatePalette()
    {
        PaletteInvalidated?.Invoke(this, EventArgs.Empty);
        Save();
    }

    /// <summary>恢复全部默认值。</summary>
    public void Reset()
    {
        AnimationOpacity = 0.92;
        PixelIntensity = 1.95;
        Saturation = 3.0;
        Contrast = 1.45;
        Brightness = 0.07;
        CellSize = 6;
        FlowSpeed = 1.0;
        CountdownDrain = true;
    }

    #region 读写

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>由插件入口调用，指定配置文件位置并载入已有设置。</summary>
    public static void Initialize(string pluginConfigFolder)
    {
        _configPath = Path.Combine(pluginConfigFolder, "options.json");
        try
        {
            if (File.Exists(_configPath) &&
                JsonSerializer.Deserialize<UltraCodeOptions>(File.ReadAllText(_configPath), JsonOptions) is { } loaded)
            {
                Current = loaded;
            }
        }
        catch (Exception)
        {
            // 配置坏了就用默认值，不要因为一个 json 拦住整个插件。
        }

        // 任何一项改动都落盘。设置页里拖滑块是高频操作，但写的是几百字节，可以接受。
        Current.PropertyChanged += (_, _) => Current.Save();
        PaletteInvalidated?.Invoke(Current, EventArgs.Empty);
    }

    public void Save()
    {
        if (_configPath is null)
        {
            return;
        }

        try
        {
            var dir = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(_configPath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception)
        {
            // 存不上就算了，下次再存。
        }
    }

    #endregion
}

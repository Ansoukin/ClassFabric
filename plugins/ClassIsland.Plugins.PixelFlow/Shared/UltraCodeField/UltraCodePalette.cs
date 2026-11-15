using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ClassIsland.UltraCodeShared;

/// <summary>
/// UltraCode 像素场的调色板。
/// </summary>
/// <remarks>
/// 这里不自己实现取色，直接接到应用已有的主题色管线上：<br/>
/// <c>Settings.ColorSource</c>（自定义 / 壁纸取色 / 屏幕取色 / 系统色）
/// → <c>MainWindow.UpdateTheme</c> → <c>IThemeService.SetTheme</c>
/// → FluentAvalonia 生成的 <c>SystemAccentColor</c> 资源。<br/>
/// 本类只做一件事：把内置的那套紫色参考色阶整体搬到主题色的色相上。
/// <para/>
/// <b>纯色模式（默认）。</b>老算法保留参考色阶的亮度、并把饱和度乘上主题色自身的饱和度，
/// 结果是两道天花板叠在一起：系统强调色的 S 通常只有 0.6 左右，等于先打六折；
/// 而 HSL 里彩度 <c>C = (1 - |2L - 1|) × S</c>，参考色阶最亮端 L 约 0.92，
/// <b>不管 S 怎么调，落到 RGB 上彩度都不超过 0.16</b>。所以饱和度滑块拉到头也还是发灰。<br/>
/// 现在改成：饱和度直接取该色相能承载的最大值，亮度压进 0.5 附近的窄带
/// （L = 0.5 正是彩度最大的地方，也就是「最鲜艳的纯色」），
/// 色阶「浅→深」的爬升形状保留，但整条都待在高彩度区。
/// 饱和度滑块变成「往纯色靠多少」的权重，调到 0 就退回老观感。
/// </remarks>
public static class UltraCodePalette
{
    // FluentAvalonia 会把主题色写进这几个键。优先取基准色，取不到再退到亮/暗变体，
    // 都取不到（比如设计器里没有加载 FluentAvaloniaTheme）就用参考色阶自己的色相。
    private static readonly string[] AccentResourceKeys =
        ["SystemAccentColor", "SystemAccentColorLight1", "SystemAccentColorDark1"];

    #region 参考色阶（原 Claude UltraCode 紫色版本）

    private static readonly Color[] ReferenceGradient =
    [
        Color.FromRgb(0xEE, 0xEB, 0xE9),
        Color.FromRgb(0xEC, 0xE9, 0xE7),
        Color.FromRgb(0xE2, 0xDC, 0xE3),
        Color.FromRgb(0xD5, 0xCA, 0xDC),
        Color.FromRgb(0xC8, 0xB5, 0xD4),
        Color.FromRgb(0xBD, 0xA6, 0xCC),
        Color.FromRgb(0xB5, 0x9B, 0xC6)
    ];

    private static readonly double[] GradientOffsets = [0.00, 0.18, 0.32, 0.48, 0.68, 0.82, 1.00];

    private static readonly Color ReferenceLeft = Color.FromRgb(210, 206, 214);
    private static readonly Color ReferenceHighlight = Color.FromRgb(216, 204, 228);
    private static readonly Color ReferencePeak = Color.FromRgb(232, 224, 242);

    private static readonly Color[] ReferenceTones =
    [
        Color.FromRgb(156, 120, 192), Color.FromRgb(156, 120, 192), // deepViolet
        Color.FromRgb(156, 132, 192), Color.FromRgb(156, 132, 192), // deepMid
        Color.FromRgb(168, 144, 204), Color.FromRgb(168, 144, 204), Color.FromRgb(168, 144, 204), // midPurple
        Color.FromRgb(168, 156, 204), Color.FromRgb(168, 156, 204), // softMid
        Color.FromRgb(180, 168, 204), // softLilac
        Color.FromRgb(192, 180, 204) // paleCool
    ];

    /// <summary>参考色阶自身的主色，用作取不到主题色时的兜底。</summary>
    private static readonly Color FallbackAccent = Color.FromRgb(0x9C, 0x78, 0xC0);

    /// <summary>纯色模式的亮度带中心。HSL 里 L=0.5 是彩度最大的位置。</summary>
    private const double VividCenter = 0.50;

    /// <summary>纯色模式的亮度带基准宽度，再乘以对比度设置。</summary>
    private const double VividSpan = 0.20;

    /// <summary>参考色阶的亮度范围，用来把原亮度线性映射进纯色亮度带。</summary>
    private static readonly double RefLightnessMin;

    private static readonly double RefLightnessMax;

    static UltraCodePalette()
    {
        var min = double.MaxValue;
        var max = double.MinValue;
        foreach (var color in AllReferenceColors())
        {
            var l = color.ToHsl().L;
            min = Math.Min(min, l);
            max = Math.Max(max, l);
        }

        RefLightnessMin = min;
        RefLightnessMax = max;
    }

    private static Color[] AllReferenceColors()
    {
        var all = new Color[ReferenceGradient.Length + ReferenceTones.Length + 3];
        ReferenceGradient.CopyTo(all, 0);
        ReferenceTones.CopyTo(all, ReferenceGradient.Length);
        all[^3] = ReferenceLeft;
        all[^2] = ReferenceHighlight;
        all[^1] = ReferencePeak;
        return all;
    }

    #endregion

    // 这些实例一经创建就不再替换，只改内容——XAML 里用 {x:Static} 拿到的是实例引用，
    // 换实例的话已经加载好的模板是不会跟着变的，改内容才能让主题色实时生效。
    private static readonly LinearGradientBrush BaseGradientBrush = new()
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative)
    };

    // 文字画笔定义在 UltraCode 的 Themes/Styles.axaml 里，这里按键名找到实例后就地改颜色。
    // 之所以不在这边新建实例：XAML 里的 StaticResource 引用在模板加载时就绑定到了具体实例上，
    // 换实例是不会生效的，改实例的 Color 才会。
    private static readonly string[] ForegroundResourceKeys =
    [
        "UltraCodeMaskForegroundBrush",
        "UltraCodeMaskForegroundSecondaryBrush",
        "UltraCodeMaskForegroundTertiaryBrush"
    ];

    // 三支画笔各自的透明度，和 XAML 里的兜底值保持一致。
    private static readonly byte[] ForegroundAlphas = [0xFF, 0xC4, 0x99];

    private static Color _appliedAccent;
    private static (double, double, double) _appliedTuning;
    private static bool _initialized;
    private static bool _foregroundApplied;

    /// <summary>像素场底下的连续渐变。主界面其余部分需要与遮罩衔接时可直接复用。</summary>
    public static IBrush BaseGradient
    {
        get
        {
            EnsureInitialized();
            return BaseGradientBrush;
        }
    }

    /// <summary>像素场最左侧的雾色。</summary>
    public static double[] LeftColor { get; private set; } = ToChannels(ReferenceLeft);

    /// <summary>像素点亮起时混入的高光色。</summary>
    public static double[] HighlightColor { get; private set; } = ToChannels(ReferenceHighlight);

    /// <summary>最亮那一档像素的颜色。</summary>
    public static double[] PeakColor { get; private set; } = ToChannels(ReferencePeak);

    /// <summary>像素场的色调环，绘制时按位置与时间在环上漂移取样。</summary>
    public static double[][] Tones { get; private set; } = Array.ConvertAll(ReferenceTones, ToChannels);

    /// <summary>色调环各通道的下界，用来约束绘制时叠加的色度抖动。</summary>
    public static double[] ToneFloor { get; private set; } = [0, 0, 0];

    /// <summary>色调环各通道的上界。</summary>
    public static double[] ToneCeiling { get; private set; } = [255, 255, 255];

    /// <summary>
    /// 压在像素场上的文字色。
    /// </summary>
    /// <remarks>
    /// 纯色模式下背景可能是任意亮度的高饱和色（纯黄很亮、纯蓝很暗），
    /// 固定的深色文字压不住。这里按背景实际的相对亮度在「近黑」「近白」之间选，
    /// 所以别的窗口要在像素场上写字，应当直接用这个值而不是自己写死。
    /// </remarks>
    public static Color ForegroundColor { get; private set; } = Color.FromRgb(0x2E, 0x1F, 0x40);

    /// <summary>文字色与背景的实测对比度。设置页里用来自证可读性。</summary>
    public static double ForegroundContrastRatio { get; private set; } = 1.0;

    /// <summary>当前生效的主题色。</summary>
    public static Color Accent => _initialized ? _appliedAccent : ResolveAccent();

    /// <summary>
    /// 重新读取主题色并刷新整套调色板。主题色没变时是空操作，可以每次播动画时无脑调用。
    /// </summary>
    public static void Refresh()
    {
        var accent = ResolveAccent();
        var tuning = TuningSignature();

        // _foregroundApplied 这个条件是给启动早期兜底的：调色板可能在主题样式挂上来之前就被取值，
        // 那时候找不到画笔实例，之后必须还有机会补上，不能被「没变化」这条给挡掉。
        if (_initialized && _foregroundApplied && accent == _appliedAccent && tuning == _appliedTuning)
        {
            return;
        }

        _appliedAccent = accent;
        _appliedTuning = tuning;
        _initialized = true;
        Apply(accent.ToHsl());
    }

    /// <summary>把影响配色的用户参数打包成一个值，用来判断要不要重算。</summary>
    private static (double, double, double) TuningSignature()
    {
        var t = UltraCodeTuning.Current;
        return (t.Saturation, t.Contrast, t.Brightness);
    }

    /// <summary>强制重算，忽略「没变化」的短路判断。设置页拖滑块时走这条。</summary>
    public static void Invalidate()
    {
        _initialized = false;
        Refresh();
    }

    private static void EnsureInitialized()
    {
        if (!_initialized)
        {
            Refresh();
        }
    }

    private static void Apply(HslColor accent)
    {
        var gradient = new Color[ReferenceGradient.Length];
        for (var i = 0; i < ReferenceGradient.Length; i++)
        {
            gradient[i] = Retint(ReferenceGradient[i], accent);
        }

        BaseGradientBrush.GradientStops.Clear();
        for (var i = 0; i < gradient.Length; i++)
        {
            BaseGradientBrush.GradientStops.Add(new GradientStop(gradient[i], GradientOffsets[i]));
        }

        LeftColor = ToChannels(Retint(ReferenceLeft, accent));
        Tones = Array.ConvertAll(ReferenceTones, tone => ToChannels(Retint(tone, accent)));
        HighlightColor = ToChannels(Retint(ReferenceHighlight, accent));
        PeakColor = ToChannels(Retint(ReferencePeak, accent));

        // 绘制时会往色调上叠一层随机色度抖动，这里给出允许游走的范围：
        // 比色调环本身略宽一点，够抖出层次，又不至于跑出这套配色的性格。
        var floor = new double[3];
        var ceiling = new double[3];
        for (var channel = 0; channel < 3; channel++)
        {
            var min = double.MaxValue;
            var max = double.MinValue;
            foreach (var tone in Tones)
            {
                min = Math.Min(min, tone[channel]);
                max = Math.Max(max, tone[channel]);
            }

            floor[channel] = Math.Max(0, min - 14);
            ceiling[channel] = Math.Min(255, max + 8);
        }

        ToneFloor = floor;
        ToneCeiling = ceiling;

        ApplyForeground(accent, gradient);
    }

    /// <summary>
    /// 按背景实际亮度挑文字色，并写进主题里那三支画笔。
    /// </summary>
    private static void ApplyForeground(HslColor accent, Color[] background)
    {
        var backgroundLuminance = 0.0;
        foreach (var color in background)
        {
            backgroundLuminance += RelativeLuminance(color);
        }

        backgroundLuminance /= Math.Max(1, background.Length);

        // 两个候选都带一点主题色相，免得文字看起来像是另一套配色里掉出来的。
        // 深色候选留高饱和（暗处彩度本来就低，留着才有色彩感）；
        // 浅色候选压低饱和，否则浅色高饱和会和背景糊在一起。
        var dark = new HslColor(1, accent.H, 0.55, 0.12).ToRgb();
        var light = new HslColor(1, accent.H, 0.16, 0.97).ToRgb();

        var darkRatio = ContrastRatio(backgroundLuminance, RelativeLuminance(dark));
        var lightRatio = ContrastRatio(backgroundLuminance, RelativeLuminance(light));

        var winner = darkRatio >= lightRatio ? dark : light;
        ForegroundColor = winner;
        ForegroundContrastRatio = Math.Max(darkRatio, lightRatio);

        var applied = true;
        for (var i = 0; i < ForegroundResourceKeys.Length; i++)
        {
            if (FindBrush(ForegroundResourceKeys[i]) is { } brush)
            {
                brush.Color = Color.FromArgb(ForegroundAlphas[i], winner.R, winner.G, winner.B);
            }
            else
            {
                applied = false;
            }
        }

        _foregroundApplied = applied;
    }

    /// <summary>
    /// 按资源键找到主题里定义的画笔实例。
    /// </summary>
    /// <remarks>
    /// 只有 UltraCode 插件的主题挂上来之后这三支画笔才存在。
    /// 别的插件按源码共享这套代码时找不到它们是正常的——
    /// 那些插件自己用 <see cref="ForegroundColor"/> 上色，不依赖资源字典。
    /// </remarks>
    private static SolidColorBrush? FindBrush(string key)
    {
        if (Application.Current is not { } app)
        {
            return null;
        }

        return app.TryFindResource(key, app.ActualThemeVariant, out var value)
            ? value as SolidColorBrush
            : null;
    }

    /// <summary>
    /// 把参考色搬到主题色的色相上。
    /// </summary>
    /// <remarks>
    /// 色相直接替换；饱和度与亮度在「老观感」和「纯色」之间按
    /// <see cref="IUltraCodeTuning.Saturation"/> 插值，到 2.0 就是完全的纯色。
    /// </remarks>
    private static Color Retint(Color source, HslColor accent)
    {
        var t = UltraCodeTuning.Current;
        var hsl = source.ToHsl();

        // 往纯色靠多少。滑块到 2.0 及以上 = 完全纯色。
        var pure = Math.Clamp(t.Saturation / 2.0, 0.0, 1.0);
        var contrast = Math.Max(0.1, t.Contrast);

        // 老算法：饱和度随主题色自身的饱和度缩放，亮度按对比度绕中灰拉开。
        var referenceSaturation =
            Math.Clamp(hsl.S * (0.30 + accent.S * 1.05) * Math.Min(t.Saturation, 1.0), 0.0, 1.0);
        var referenceLightness = 0.5 + (hsl.L - 0.5) * contrast + t.Brightness;

        // 纯色算法：饱和度拉满；亮度线性映射进 0.5 附近的窄带，保住彩度。
        var extent = RefLightnessMax - RefLightnessMin;
        var normalized = extent < 1e-6 ? 0.5 : (hsl.L - RefLightnessMin) / extent;
        var span = Math.Clamp(VividSpan * contrast, 0.04, 0.46);
        var vividLightness = VividCenter + t.Brightness + (normalized - 0.5) * span;

        var saturation = Math.Clamp(Mix(referenceSaturation, 1.0, pure), 0.0, 1.0);
        var lightness = Math.Clamp(Mix(referenceLightness, vividLightness, pure), 0.0, 1.0);

        return new HslColor(hsl.A, accent.H, saturation, lightness).ToRgb();
    }

    private static Color ResolveAccent()
    {
        if (Application.Current is not { } app)
        {
            return FallbackAccent;
        }

        foreach (var key in AccentResourceKeys)
        {
            if (!app.TryFindResource(key, app.ActualThemeVariant, out var value))
            {
                continue;
            }

            switch (value)
            {
                case Color color:
                    return color;
                case SolidColorBrush brush:
                    return brush.Color;
            }
        }

        return FallbackAccent;
    }

    #region 对比度

    /// <summary>WCAG 相对亮度。</summary>
    private static double RelativeLuminance(Color color) =>
        0.2126 * ToLinear(color.R) + 0.7152 * ToLinear(color.G) + 0.0722 * ToLinear(color.B);

    private static double ToLinear(byte channel)
    {
        var v = channel / 255.0;
        return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }

    private static double ContrastRatio(double a, double b)
    {
        var hi = Math.Max(a, b);
        var lo = Math.Min(a, b);
        return (hi + 0.05) / (lo + 0.05);
    }

    #endregion

    private static double Mix(double from, double to, double amount) => from + (to - from) * amount;

    private static double[] ToChannels(Color color) => [color.R, color.G, color.B];
}

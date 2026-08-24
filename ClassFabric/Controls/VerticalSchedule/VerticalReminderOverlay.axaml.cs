using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using ClassFabric.Core.Abstractions.Services;
using ClassFabric.Shared;

namespace ClassFabric.Controls.VerticalSchedule;

/// <summary>
/// 竖条上下课提醒覆盖层：实心强调色底板＋五个提示画面（下课提示占三拍）。
/// 本控件只负责视觉呈现，页面停留时长等编舞决策全部由宿主窗口的状态机驱动。
/// </summary>
public partial class VerticalReminderOverlay : UserControl
{
    public enum ReminderPage
    {
        BreakingLabel,
        BreakingNextLessonLabel,
        BreakingNextLessonFullName,
        OnClassIncoming,
        OnClassFullName,
    }

    /// <summary>
    /// 当前页面的逐字入场动画全部排程完毕时触发。
    /// </summary>
    public event EventHandler? PageIntroCompleted;

    private ILessonsService LessonsService { get; } = IAppHost.GetService<ILessonsService>();

    private CancellationTokenSource? _introCts;
    private Border? _activePageRoot;

    // 底板进出的透明度动画时长（编舞链阶段计时的唯一来源，宿主窗口引用此常量）
    public const int PlateFadeMs = 250;

    // 逐字入场：每字间隔与单字动画时长
    private const int CharStaggerMs = 100;
    private const int CharAnimMs = 300;

    // 各画面固定文案；高度估算与构建共用，保证口径一致
    private const string BreakingLabelText = "现在是下课时间";
    private const string NextLessonLabelText = "下一节课是";
    private const string IncomingLabelText = "即将上课";

    public VerticalReminderOverlay()
    {
        InitializeComponent();
        PlateRoot.Opacity = 0;
        PlateRoot.IsVisible = false;
    }

    /// <summary>
    /// 底板淡入。动画时长属自身视觉序列，与停留时长决策无关。
    /// </summary>
    public async Task ShowPlateAsync()
    {
        PlateRoot.IsVisible = true;
        await AnimatePlateOpacityAsync(0, 1);
    }

    /// <summary>
    /// 底板淡出并折叠。从当前透明度起跳：打断可能发生在淡入途中或底板尚未
    /// 显示时，固定从 1 起跳会让空白底板闪现一下。
    /// </summary>
    public async Task HidePlateAsync()
    {
        if (!PlateRoot.IsVisible || PlateRoot.Opacity <= 0)
        {
            PlateRoot.IsVisible = false;
            PlateRoot.Opacity = 0;
            return;
        }
        await AnimatePlateOpacityAsync(PlateRoot.Opacity, 0);
        PlateRoot.IsVisible = false;
    }

    private async Task AnimatePlateOpacityAsync(double from, double to)
    {
        var animation = new Animation
        {
            FillMode = FillMode.Both,
            Duration = TimeSpan.FromMilliseconds(PlateFadeMs),
            Children =
            {
                new KeyFrame
                {
                    KeyTime = TimeSpan.Zero,
                    Setters = { new Setter(OpacityProperty, from) }
                },
                new KeyFrame
                {
                    KeyTime = TimeSpan.FromMilliseconds(PlateFadeMs),
                    Setters = { new Setter(OpacityProperty, to) }
                },
            }
        };
        await animation.RunAsync(PlateRoot);
        PlateRoot.Opacity = to;
    }

    /// <summary>
    /// 切换到指定画面并重播逐字入场动画。科目信息在调用时实时读取课表服务，不缓存。
    /// 取消粒度为页面级：切页取消旧页全部排程，同页多组文字并行不互相取消。
    /// </summary>
    public void ShowPage(ReminderPage page)
    {
        _introCts?.Cancel();
        _introCts = new CancellationTokenSource();

        var (root, build) = page switch
        {
            ReminderPage.BreakingLabel => (PageA1, BuildBreakingLabelPage()),
            ReminderPage.BreakingNextLessonLabel => (PageA2Label, BuildBreakingNextLessonLabelPage()),
            ReminderPage.BreakingNextLessonFullName => (PageA2Name, BuildBreakingNextLessonFullNamePage()),
            ReminderPage.OnClassIncoming => (PageB1, BuildOnClassIncomingPage()),
            ReminderPage.OnClassFullName => (PageB2, BuildOnClassFullNamePage()),
            _ => throw new ArgumentOutOfRangeException(nameof(page)),
        };

        foreach (var border in new[] { PageA1, PageA2Label, PageA2Name, PageB1, PageB2 })
        {
            border.Classes.Remove("active");
            border.Classes.Remove("fading");
        }
        root.Classes.Add("active");
        _activePageRoot = root;
        build();
    }

    /// <summary>
    /// 当前页面整体淡出：撤下激活类后透明度经过渡回落，fading 类维持其
    /// 可见性直到过渡结束，避免淡出途中被直接折叠。
    /// </summary>
    public void FadeOutCurrentPage()
    {
        if (_activePageRoot == null) return;
        _activePageRoot.Classes.Remove("active");
        _activePageRoot.Classes.Add("fading");
        _activePageRoot = null;
    }

    /// <summary>
    /// 页面淡出过渡结束后调用：撤下 fading 类正式折叠全部页面。
    /// </summary>
    public void CompletePageFadeOut()
    {
        foreach (var border in new[] { PageA1, PageA2Label, PageA2Name, PageB1, PageB2 })
        {
            border.Classes.Remove("fading");
        }
    }

    /// <summary>
    /// 中止路径（上课铃打断、运行中关闭开关）：不经淡出过渡直接折叠全部页面。
    /// </summary>
    public void AbortPages()
    {
        _introCts?.Cancel();
        foreach (var border in new[] { PageA1, PageA2Label, PageA2Name, PageB1, PageB2 })
        {
            border.Classes.Remove("active");
            border.Classes.Remove("fading");
        }
        _activePageRoot = null;
    }

    private Action BuildBreakingLabelPage()
    {
        return () =>
        {
            PlayIntro(BuildChars(A1Text, BreakingLabelText, "MainWindowBodyFontSize"), 1.0);
        };
    }

    private Action BuildBreakingNextLessonLabelPage()
    {
        return () =>
        {
            PlayIntro(BuildChars(A2LabelText, NextLessonLabelText, "MainWindowEmphasizedFontSize"), 1.0);
        };
    }

    private Action BuildBreakingNextLessonFullNamePage()
    {
        return () =>
        {
            // 竖排字号键由窗口按可用高度注入，超高时自动缩级（与上课全名页同款）
            PlayIntro(BuildChars(A2NameText, LessonsService.NextClassSubject.Name, "VerticalReminderVerticalFontSize"), 1.0);
        };
    }

    private Action BuildOnClassIncomingPage()
    {
        return () =>
        {
            PlayIntro(BuildChars(B1Text, IncomingLabelText, "MainWindowEmphasizedFontSize"), 1.0);
        };
    }

    private Action BuildOnClassFullNamePage()
    {
        return () =>
        {
            // 竖排字号键由窗口按可用高度注入，超高时自动缩级
            PlayIntro(BuildChars(B2Chars, LessonsService.NextClassSubject.Name, "VerticalReminderVerticalFontSize"), 1.0);
        };
    }

    /// <summary>
    /// 将竖排页已构建字块的字号同步为指定值（缩级后即时生效，不重播入场动画）。
    /// 下课全名页与上课全名页共用同一竖排字号键，两处一并同步。
    /// </summary>
    public void ApplyVerticalFontSize(double fontSize)
    {
        foreach (var container in new[] { A2NameText, B2Chars })
        {
            foreach (var child in container.Children)
            {
                if (child is TextBlock block)
                {
                    block.FontSize = fontSize;
                }
            }
        }
    }

    /// <summary>
    /// 估算指定画面所需的内容高度（逻辑像素），供窗口在高度公式内取 max。
    /// 四个画面均为竖排单列：文字高＝字数×字号×行距系数。
    /// </summary>
    public double EstimateRequiredHeight(ReminderPage page)
    {
        var icon = TryFindResourceDouble("VerticalSchedulePlaceholderIconSize", 28);
        var body = TryFindResourceDouble("MainWindowBodyFontSize", 14);
        var emphasized = TryFindResourceDouble("MainWindowEmphasizedFontSize", 18);
        const double spacing = 8;
        const double lineFactor = 1.2;
        const double margin = 24;

        return page switch
        {
            ReminderPage.BreakingLabel => icon + spacing + BreakingLabelText.Length * body * lineFactor + margin,
            ReminderPage.BreakingNextLessonLabel => icon + spacing + NextLessonLabelText.Length * emphasized * lineFactor + margin,
            ReminderPage.BreakingNextLessonFullName => icon + spacing + Math.Max(1, LessonsService.NextClassSubject.Name.Length)
                                                       * TryFindResourceDouble("VerticalReminderVerticalFontSize", 20) * lineFactor + margin,
            ReminderPage.OnClassIncoming => icon + spacing + IncomingLabelText.Length * emphasized * lineFactor + margin,
            ReminderPage.OnClassFullName => icon + spacing + LessonsService.NextClassSubject.Name.Length * TryFindResourceDouble("VerticalReminderVerticalFontSize", 20) * lineFactor + margin,
            _ => 0,
        };
    }

    private double TryFindResourceDouble(string key, double fallback)
    {
        return this.TryFindResource(key, out var value) && value is double d ? d : fallback;
    }

    private List<TextBlock> BuildChars(Panel container, string text, string fontSizeKey)
    {
        container.Children.Clear();
        var fontSize = TryFindResourceDouble(fontSizeKey, 14);
        var chars = new List<TextBlock>();
        foreach (var c in text)
        {
            var block = new TextBlock
            {
                Text = c.ToString(),
                FontSize = fontSize,
                Opacity = 0,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                RenderTransform = new TranslateTransform(0, 8),
            };
            if (c == '：' || c == ':')
            {
                // 竖排流中的冒号旋转 90°，两点呈横向排布贴合竖列阅读习惯；
                // 经 LayoutTransformControl 参与布局，逐字动画仍作用于内层文字块
                container.Children.Add(new LayoutTransformControl
                {
                    LayoutTransform = new RotateTransform(90d),
                    Child = block,
                });
            }
            else
            {
                container.Children.Add(block);
            }
            chars.Add(block);
        }
        return chars;
    }

    /// <summary>
    /// 逐字入场排程：每字约 100ms 间隔，透明度渐显并自下方轻微上浮。
    /// 使用当前页面的取消令牌，切页时统一作废。
    /// </summary>
    private void PlayIntro(List<TextBlock> chars, double finalOpacity, int startDelayMs = 0)
    {
        _ = PlayIntroAsync(chars, finalOpacity, startDelayMs, _introCts?.Token ?? CancellationToken.None);
    }

    private async Task PlayIntroAsync(List<TextBlock> chars, double finalOpacity, int startDelayMs, CancellationToken token)
    {
        try
        {
            if (startDelayMs > 0)
            {
                await Task.Delay(startDelayMs, token);
            }
            foreach (var block in chars)
            {
                if (token.IsCancellationRequested) return;
                block.Opacity = finalOpacity;
                var animation = new Animation
                {
                    FillMode = FillMode.Both,
                    Duration = TimeSpan.FromMilliseconds(CharAnimMs),
                    Children =
                    {
                        new KeyFrame
                        {
                            KeyTime = TimeSpan.Zero,
                            Setters =
                            {
                                new Setter(OpacityProperty, 0.0),
                                new Setter(TranslateTransform.YProperty, 8.0),
                            }
                        },
                        new KeyFrame
                        {
                            KeyTime = TimeSpan.FromMilliseconds(CharAnimMs),
                            Setters =
                            {
                                new Setter(OpacityProperty, finalOpacity),
                                new Setter(TranslateTransform.YProperty, 0.0),
                            }
                        },
                    }
                };
                _ = animation.RunAsync(block);
                await Task.Delay(CharStaggerMs, token);
            }
            PageIntroCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            // 快速切页时丢弃旧排程
        }
    }
}

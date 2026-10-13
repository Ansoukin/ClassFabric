using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace ClassIsland.Controls;

/// <summary>
/// Fluent 风格翻页指示器（WinUI PipsPager 的轻量自绘版）：
/// 雪佛龙 + pip 条合并成一条胶囊，浮在轮播图底边；当前页 pip 拉长为胶囊。
/// 纯代码构建视觉树，不走 ControlTemplate——pip 选中态要在代码里精确控制，模板绑定反而绕。
/// 放 app 项目不放 Core：Core 新增公开类型必须补 TypeForwardedTo，UI 控件犯不着动兼容层。
/// </summary>
public class PipsPager : ContentControl
{
    private readonly StackPanel _root = new() { Orientation = Orientation.Horizontal, Spacing = 4 };
    private readonly Button _prevButton;
    private readonly Button _nextButton;
    private readonly System.Collections.Generic.List<Button> _pips = new();

    public static readonly StyledProperty<int> PageCountProperty =
        AvaloniaProperty.Register<PipsPager, int>(nameof(PageCount));

    /// <summary>越界值会被夹回有效范围，绑定源可以随便写。必须 TwoWay：控件上点选要推回轮播数据源。</summary>
    public static readonly StyledProperty<int> SelectedPageIndexProperty =
        AvaloniaProperty.Register<PipsPager, int>(nameof(SelectedPageIndex),
            defaultBindingMode: BindingMode.TwoWay, coerce: CoerceIndex);

    /// <summary>用户或绑定改动选中页后触发，宿主用它切换轮播图。</summary>
    public event EventHandler<int>? SelectedIndexChanged;

    public int PageCount
    {
        get => GetValue(PageCountProperty);
        set => SetValue(PageCountProperty, value);
    }

    public int SelectedPageIndex
    {
        get => GetValue(SelectedPageIndexProperty);
        set => SetValue(SelectedPageIndexProperty, value);
    }

    /// <summary>单图没有可翻的东西，整条隐藏。</summary>
    public bool ShowPager => PageCount > 1;

    static PipsPager()
    {
        PageCountProperty.Changed.AddClassHandler<PipsPager>((x, _) => x.RebuildPips());
        SelectedPageIndexProperty.Changed.AddClassHandler<PipsPager>((x, _) =>
        {
            x.RefreshSelection();
            x.SelectedIndexChanged?.Invoke(x, x.SelectedPageIndex);
        });
    }

    public PipsPager()
    {
        _prevButton = MakeNavButton("‹");
        _prevButton.Click += (_, _) => Move(-1);
        _nextButton = MakeNavButton("›");
        _nextButton.Click += (_, _) => Move(1);

        // 用半透明深色胶囊而不是实时 Acrylic：叠在图片上开模糊开销大，视觉上纯色足够接近
        var capsule = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#B3202020")),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(10, 4),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 10),
            Child = _root
        };
        Content = capsule;
    }

    private static int CoerceIndex(AvaloniaObject sender, int value) => Math.Max(0, value);

    private Button MakeNavButton(string glyph)
    {
        var b = new Button
        {
            Content = new TextBlock { Text = glyph, FontSize = 14 },
            Width = 20,
            Height = 20,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0)
        };
        _root.Children.Add(b);
        return b;
    }

    private void Move(int delta)
    {
        var target = SelectedPageIndex + delta;
        if (target < 0 || target >= PageCount)
            return;
        SetCurrentValue(SelectedPageIndexProperty, target);
    }

    private void RebuildPips()
    {
        // 雪佛龙固定在两端，重建时只动中间的 pip
        while (_root.Children.Count > 2)
        {
            _root.Children.RemoveAt(1);
        }
        _pips.Clear();

        // 页数收缩时旧选中下标可能越界，先夹回去（否则 prev 按钮看着能点却永远 no-op）
        if (SelectedPageIndex >= PageCount)
        {
            SetCurrentValue(SelectedPageIndexProperty, Math.Max(0, PageCount - 1));
        }

        for (var i = 0; i < PageCount; i++)
        {
            var index = i;
            var bar = new Rectangle
            {
                Width = 12,
                Height = 4,
                RadiusX = 2,
                RadiusY = 2,
                Fill = Brushes.White,
                Opacity = 0.45
            };
            var pip = new Button
            {
                Content = bar,
                Width = 20,
                Height = 20,
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };
            pip.Click += (_, _) => SetCurrentValue(SelectedPageIndexProperty, index);
            _root.Children.Insert(1 + i, pip);
            _pips.Add(pip);
        }

        IsVisible = ShowPager;
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        for (var i = 0; i < _pips.Count; i++)
        {
            var active = i == SelectedPageIndex;
            var bar = (Rectangle)_pips[i].Content!;
            bar.Width = active ? 18 : 12;
            bar.Opacity = active ? 1 : 0.45;
        }
        _prevButton.IsEnabled = SelectedPageIndex > 0;
        _nextButton.IsEnabled = SelectedPageIndex < PageCount - 1;
    }
}

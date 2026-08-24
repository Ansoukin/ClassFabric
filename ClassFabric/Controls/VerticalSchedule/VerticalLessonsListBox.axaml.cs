using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ClassFabric.Shared.ComponentModels;
using ClassFabric.Shared.Models.Profile;

namespace ClassFabric.Controls.VerticalSchedule;

/// <summary>
/// 竖版课表盲字列表。数据直连 LessonsService 管线（课表/科目/当前时间点由外部绑定注入，
/// 本控件不持有任何课表快照），视口自动以当前时间点为中心。
/// </summary>
public partial class VerticalLessonsListBox : UserControl
{
    public static readonly StyledProperty<ClassPlan?> ClassPlanProperty =
        AvaloniaProperty.Register<VerticalLessonsListBox, ClassPlan?>(nameof(ClassPlan));

    public ClassPlan? ClassPlan
    {
        get => GetValue(ClassPlanProperty);
        set => SetValue(ClassPlanProperty, value);
    }

    public static readonly StyledProperty<ObservableOrderedDictionary<Guid, Subject>?> SubjectsProperty =
        AvaloniaProperty.Register<VerticalLessonsListBox, ObservableOrderedDictionary<Guid, Subject>?>(nameof(Subjects));

    public ObservableOrderedDictionary<Guid, Subject>? Subjects
    {
        get => GetValue(SubjectsProperty);
        set => SetValue(SubjectsProperty, value);
    }

    public static readonly StyledProperty<TimeLayoutItem?> CurrentTimeLayoutItemProperty =
        AvaloniaProperty.Register<VerticalLessonsListBox, TimeLayoutItem?>(nameof(CurrentTimeLayoutItem));

    public TimeLayoutItem? CurrentTimeLayoutItem
    {
        get => GetValue(CurrentTimeLayoutItemProperty);
        set => SetValue(CurrentTimeLayoutItemProperty, value);
    }

    public static readonly StyledProperty<int> RefreshTokenProperty =
        AvaloniaProperty.Register<VerticalLessonsListBox, int>(nameof(RefreshToken));

    /// <summary>
    /// 课表内容变更令牌。课表内部的课程信息变化（换课、增删节次等）不会体现在属性绑定上，
    /// 递增此值触发单元格的 MultiBinding 重新求值。
    /// </summary>
    public int RefreshToken
    {
        get => GetValue(RefreshTokenProperty);
        private set => SetValue(RefreshTokenProperty, value);
    }

    public static readonly StyledProperty<TimeLayoutItem?> FirstRenderableItemProperty =
        AvaloniaProperty.Register<VerticalLessonsListBox, TimeLayoutItem?>(nameof(FirstRenderableItem));

    /// <summary>
    /// 渲染判定后的首个可渲染项（分隔/课间/隐藏占位不计），用于首格端部留白样式。
    /// </summary>
    public TimeLayoutItem? FirstRenderableItem
    {
        get => GetValue(FirstRenderableItemProperty);
        private set => SetValue(FirstRenderableItemProperty, value);
    }

    public static readonly StyledProperty<TimeLayoutItem?> LastRenderableItemProperty =
        AvaloniaProperty.Register<VerticalLessonsListBox, TimeLayoutItem?>(nameof(LastRenderableItem));

    /// <summary>
    /// 渲染判定后的末个可渲染项，用于末格端部留白与末格不画分隔线的判定。
    /// </summary>
    public TimeLayoutItem? LastRenderableItem
    {
        get => GetValue(LastRenderableItemProperty);
        private set => SetValue(LastRenderableItemProperty, value);
    }

    public static readonly StyledProperty<bool> HasRenderableItemsProperty =
        AvaloniaProperty.Register<VerticalLessonsListBox, bool>(nameof(HasRenderableItems));

    /// <summary>
    /// 是否存在可渲染格；为假时窗口显示空课表占位。
    /// </summary>
    public bool HasRenderableItems
    {
        get => GetValue(HasRenderableItemsProperty);
        private set => SetValue(HasRenderableItemsProperty, value);
    }

    public static readonly StyledProperty<bool> HighlightChangedClassProperty =
        AvaloniaProperty.Register<VerticalLessonsListBox, bool>(nameof(HighlightChangedClass));

    /// <summary>
    /// 是否点亮换课高亮标记。由窗口从应用设置绑定注入，与单元格的换课判定
    /// 双条件合成后驱动划线显示。
    /// </summary>
    public bool HighlightChangedClass
    {
        get => GetValue(HighlightChangedClassProperty);
        set => SetValue(HighlightChangedClassProperty, value);
    }

    public VerticalLessonsListBox()
    {
        InitializeComponent();
        this.GetObservable(ClassPlanProperty).Subscribe(_ => OnClassPlanChanged());
        this.GetObservable(CurrentTimeLayoutItemProperty).Subscribe(_ => OnRenderableItemsChanged());
        this.GetObservable(RefreshTokenProperty).Subscribe(_ => OnRenderableItemsChanged());
        AttachedToVisualTree += (_, _) => ScheduleScrollToCurrent();
    }

    private ClassPlan? _attachedPlan;

    private void OnClassPlanChanged()
    {
        // 课表实例切换时重挂集合变更监听，保证运行中编辑节次即时刷新并重新对中
        if (_attachedPlan != null)
        {
            _attachedPlan.ClassesChanged -= AttachedPlanOnClassesChanged;
            DetachLayoutsChanged(_attachedPlan);
        }
        _attachedPlan = ClassPlan;
        if (_attachedPlan != null)
        {
            _attachedPlan.ClassesChanged += AttachedPlanOnClassesChanged;
            AttachLayoutsChanged(_attachedPlan);
        }
        OnRenderableItemsChanged();
        ScheduleScrollToCurrent();
    }

    private void AttachedPlanOnClassesChanged(object? sender, EventArgs e)
    {
        RefreshToken++;
    }

    private void AttachLayoutsChanged(ClassPlan? classPlan)
    {
        var layouts = classPlan?.TimeLayout?.Layouts;
        if (layouts == null) return;
        layouts.CollectionChanged += LayoutsOnCollectionChanged;
    }

    private void DetachLayoutsChanged(ClassPlan? classPlan)
    {
        var layouts = classPlan?.TimeLayout?.Layouts;
        if (layouts == null) return;
        layouts.CollectionChanged -= LayoutsOnCollectionChanged;
    }

    private void LayoutsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnRenderableItemsChanged();
        ScheduleScrollToCurrent();
    }

    /// <summary>
    /// 判定一个时间点当前是否占一个列表槽位（含分隔槽），与模板选择转换器的分支保持一致。
    /// 供窗口高度统计与全天进度轨的槽位口径消费，三处必须同源。
    /// </summary>
    internal static bool IsRenderable(TimeLayoutItem item, ICollection<TimeLayoutItem> valid, TimeLayoutItem? current)
    {
        if (item.TimeType == 3) return false;
        if (!valid.Contains(item)) return false;
        if (item.TimeType == 1) return false; // 课间一律不渲染
        if (item.IsHideDefault && current != item) return false; // 默认隐藏项仅非当前时隐藏
        return true;
    }

    /// <summary>
    /// 判定一个时间点是否为上课格（首末格与空表状态只按上课格计，分隔槽不参与）。
    /// </summary>
    private static bool IsLessonItem(TimeLayoutItem item, ICollection<TimeLayoutItem> valid, TimeLayoutItem? current)
    {
        return item.TimeType == 0 && IsRenderable(item, valid, current);
    }

    /// <summary>
    /// 依据上课格判定重算首末格与空表状态（分隔槽不参与首末与空表口径）。
    /// 课表实例、当前时间点、内容刷新令牌任一变化后调用，
    /// 供端部留白/空占位样式消费。
    /// </summary>
    private void OnRenderableItemsChanged()
    {
        var plan = ClassPlan;
        var layouts = plan?.TimeLayout?.Layouts;
        if (layouts == null)
        {
            FirstRenderableItem = null;
            LastRenderableItem = null;
            HasRenderableItems = false;
            return;
        }

        ICollection<TimeLayoutItem> valid = plan!.ValidTimeLayoutItems;
        var current = CurrentTimeLayoutItem;
        TimeLayoutItem? first = null;
        TimeLayoutItem? last = null;
        foreach (var item in layouts)
        {
            if (!IsLessonItem(item, valid, current)) continue;
            first ??= item;
            last = item;
        }

        FirstRenderableItem = first;
        LastRenderableItem = last;
        HasRenderableItems = first != null;
        ScheduleScrollToCurrent();
    }

    /// <summary>
    /// 按占槽口径统计当前会渲染出实际高度的槽位数量（含分隔槽），
    /// 供窗口按“槽数×格高”计算内容期望高度（不依赖布局量测，隐藏状态下同样准确）。
    /// </summary>
    public int GetRenderableItemCount()
    {
        var plan = ClassPlan;
        var layouts = plan?.TimeLayout?.Layouts;
        if (layouts == null) return 0;
        ICollection<TimeLayoutItem> valid = plan!.ValidTimeLayoutItems;
        var current = CurrentTimeLayoutItem;
        var count = 0;
        foreach (var item in layouts)
        {
            if (IsRenderable(item, valid, current)) count++;
        }
        return count;
    }

    private void ScheduleScrollToCurrent()
    {
        // 容器生成晚于数据变更，等布局完成后再定位
        Dispatcher.UIThread.Post(ScrollToCurrent, DispatcherPriority.Loaded);
    }

    private void ScrollToCurrent()
    {
        var item = CurrentTimeLayoutItem;
        if (item == null) return;
        if (InnerListBox.ContainerFromItem(item) is not Control container) return;
        var target = container.Bounds.Top + container.Bounds.Height / 2 - PART_ScrollViewer.Viewport.Height / 2;
        if (target < 0) target = 0;
        PART_ScrollViewer.Offset = new Vector(0, target);
    }
}

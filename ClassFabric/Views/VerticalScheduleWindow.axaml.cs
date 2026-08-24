using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using ClassFabric.Controls.VerticalSchedule;
using ClassFabric.Core.Abstractions.Services;
using ClassFabric.Core.Helpers.UI;
using ClassFabric.Platforms.Abstraction;
using ClassFabric.Platforms.Abstraction.Enums;
using ClassFabric.Services;
using ClassFabric.Shared.Enums;
using ClassFabric.Shared.Models.Profile;
using Microsoft.Extensions.Logging;

namespace ClassFabric.Views;

/// <summary>
/// 竖版课表窗口（Beta）：透明无边框工具窗，贴屏幕左/右缘，点击穿透常开。
/// </summary>
public partial class VerticalScheduleWindow : Window
{
    private ILogger<VerticalScheduleWindow> Logger { get; }
    private SettingsService SettingsService { get; }
    private IRulesetService RulesetService { get; }
    private ILessonsService LessonsService { get; }
    private IProfileService ProfileService { get; }
    private IExactTimeService ExactTimeService { get; }

    // 单个盲字单元格的基准高度；实际格高为基准值乘全局缩放
    private const double CellHeight = 40;

    // 空课表占位（图标＋文案）的内容需求高度基准
    private const double PlaceholderHeight = 64;

    // 显隐动画时长（淡入淡出＋停靠缘滑入滑出）
    private static readonly TimeSpan ShowHideAnimationDuration = TimeSpan.FromMilliseconds(180);

    // 滑入滑出位移基准（乘全局缩放）
    private const double SlideDistance = 12;

    public bool IsForegroundFullscreen { get; private set; } = false;
    public bool IsForegroundMaxWindow { get; private set; } = false;

    private bool _lastVisibilityApplied = true;
    private readonly DispatcherTimer _hideDeferredTimer = new();

    public VerticalScheduleWindow(ILogger<VerticalScheduleWindow> logger,
        SettingsService settingsService,
        IRulesetService rulesetService,
        ILessonsService lessonsService,
        IProfileService profileService,
        IExactTimeService exactTimeService)
    {
        Logger = logger;
        SettingsService = settingsService;
        RulesetService = rulesetService;
        LessonsService = lessonsService;
        ProfileService = profileService;
        ExactTimeService = exactTimeService;
        InitializeComponent();
        DataContext = SettingsService.Settings;
        RulesetService.StatusUpdated += RulesetServiceOnStatusUpdated;
        LessonsService.PropertyChanged += LessonsServiceOnPropertyChanged;
        LessonsService.OnBreakingTime += LessonsServiceOnBreakingTime;
        SettingsService.Settings.PropertyChanged += SettingsOnPropertyChanged;
        _hideDeferredTimer.Interval = ShowHideAnimationDuration;
        _hideDeferredTimer.Tick += (_, _) =>
        {
            _hideDeferredTimer.Stop();
            // 动画播完后再折叠整树，期间保持可见让退场动画完整呈现
            if (!_lastVisibilityApplied)
            {
                WindowRoot.IsVisible = false;
            }
        };
        UpdateTypographyResources();
        UpdateCornerRadiusResource();
        UpdateForegroundColor();
    }

    private Screen? GetSelectedScreenSafe()
    {
        // 逻辑同主窗 GetSelectedScreenSafe，索引越界回退主屏
        var index = SettingsService.Settings.WindowDockingMonitorIndex;
        return index < Screens.ScreenCount && index >= 0
            ? Screens.All[index]
            : Screens.Primary;
    }

    /// <summary>
    /// 定位公式：贴左/右缘、垂直居中加 OffsetY；宽度钳制到不超过工作区宽。
    /// 窗口尺寸走 SizeToContent 由内容自然驱动（横条同款，缩放与内容变更单帧原子
    /// 生效，杜绝手工赋尺寸与内容变换两段式的旧帧闪烁），此处只管定位与高度封顶：
    /// 封顶经 WindowRoot.MaxHeight 交给布局系统钳制，位置重定位所需的期望高仍按
    /// 内容口径估算。Avalonia 窗口 Position 为物理像素，故经 dpi 换算。
    /// </summary>
    public void UpdateWindowPos()
    {
        var screen = GetSelectedScreenSafe();
        if (screen == null) return;
        var settings = SettingsService.Settings;
        var dpi = screen.Scaling;
        var work = settings.IsIgnoreWorkAreaEnabled ? screen.Bounds : screen.WorkingArea;
        var vsSettings = settings.VerticalScheduleSettings;
        var w = Math.Min(vsSettings.Thickness * settings.Scale * vsSettings.Zoom, work.Width / dpi);
        var cellHeight = CellHeight * settings.Scale * vsSettings.Zoom;
        // 空课表时按占位内容的需求高度撑起胶囊，避免提示被裁剪
        var contentHeight = ScheduleList.HasRenderableItems
            ? ScheduleList.GetRenderableItemCount() * cellHeight
            : PlaceholderHeight * settings.Scale * vsSettings.Zoom;
        var availableHeight = work.Height / dpi;
        // 窗口尺寸走 SizeToContent 由内容自然驱动（横条同款，缩放与内容变更单帧原子生效），
        // 此处不再手工赋宽高。MinHeight＝课表自身高度地板：提醒状态机会把列表 IsVisible=false，
        // 没有地板时窗口会塌缩到提醒画面自身的自然高；MaxHeight＝工作区封顶，超高内部滚动
        SetResourceValue("VerticalScheduleWindowMinHeight", Math.Min(contentHeight, availableHeight));
        SetResourceValue("VerticalScheduleWindowMaxHeight", availableHeight);
        // 提醒画面显示期间按画面需求临时增高，封顶工作区高；课表淡入段即回落
        if (_reminderPhase is ReminderPhase.ScheduleFadeOut or ReminderPhase.PlateIn
            or ReminderPhase.Page or ReminderPhase.PageOut or ReminderPhase.PlateOut)
        {
            UpdateVerticalReminderFont(availableHeight);
            // 估算基于变换树内的原始值，与窗口逻辑尺寸比较前需乘生效缩放
            var reminderHeight = ReminderOverlay.EstimateRequiredHeight(_currentPage) * GetContentScale();
            contentHeight = Math.Max(contentHeight, Math.Min(reminderHeight, availableHeight));
        }
        var wPhys = w * dpi;
        var hPhys = contentHeight * dpi;
        var x = settings.VerticalScheduleSettings.DockingEdge == 0
            ? work.X
            : work.Right - wPhys;
        var y = work.Y + (work.Height - hPhys) / 2 + settings.VerticalScheduleSettings.OffsetY;
        var newPos = new PixelPoint((int)x, (int)y);
        if (Position != newPos) Position = newPos;
    }

    /// <summary>
    /// 显隐：多源合成单一求值函数。规则性隐藏＝整树折叠（含着色层），窗口本体不动（复刻主窗语义）。
    /// 状态翻转时播放一次淡入淡出＋停靠缘滑入滑出动画；状态不变时（定时器重断言）不重放。
    /// </summary>
    public void UpdateVisibility()
    {
        var settings = SettingsService.Settings;
        var isHidden =
            !settings.IsMainWindowVisible ||
            (settings.HideOnClass && LessonsService.CurrentState == TimeState.OnClass) ||
            (settings.HideOnFullscreen && IsForegroundFullscreen) ||
            (settings.HideOnMaxWindow && IsForegroundMaxWindow);
        var visible = !isHidden;
        if (visible == _lastVisibilityApplied)
        {
            return;
        }

        _lastVisibilityApplied = visible;
        _hideDeferredTimer.Stop();
        if (visible)
        {
            WindowRoot.IsVisible = true;
            PlayShowHideAnimation(entering: true);
        }
        else
        {
            PlayShowHideAnimation(entering: false);
            // 延后折叠，让退场动画完整播放；动画被禁时立即折叠
            if (IThemeService.AnimationLevel >= 2)
            {
                _hideDeferredTimer.Start();
            }
            else
            {
                WindowRoot.IsVisible = false;
            }
        }
    }

    /// <summary>
    /// 显隐组合动画：整树透明度渐变＋自停靠缘滑入滑出。
    /// 视觉树合成动画不阻塞布局，定时器的定位与层级断言照常执行。
    /// </summary>
    private void PlayShowHideAnimation(bool entering)
    {
        if (IThemeService.AnimationLevel < 2)
        {
            return;
        }

        var visual = ElementComposition.GetElementVisual(WindowRoot);
        if (visual == null)
        {
            return;
        }

        var compositor = visual.Compositor;
        var scale = SettingsService.Settings.Scale;
        // 停靠右缘向右滑出，停靠左缘向左滑出；入场取反方向
        var slideX = (SettingsService.Settings.VerticalScheduleSettings.DockingEdge == 0 ? -1 : 1) * SlideDistance * scale;

        var opacityAnim = compositor.CreateScalarKeyFrameAnimation();
        opacityAnim.InsertKeyFrame(0f, entering ? 0f : 1f);
        opacityAnim.InsertKeyFrame(1f, entering ? 1f : 0f, Avalonia.Animation.Easings.Easing.Parse(".65,0,.35,1.0"));
        opacityAnim.Duration = ShowHideAnimationDuration;
        opacityAnim.Target = nameof(visual.Opacity);
        visual.StartAnimation(nameof(visual.Opacity), opacityAnim);

        var offsetAnim = compositor.CreateVector3DKeyFrameAnimation();
        offsetAnim.InsertKeyFrame(0f, entering ? new System.Numerics.Vector3((float)slideX, 0f, 0f) : new System.Numerics.Vector3(0f, 0f, 0f));
        offsetAnim.InsertKeyFrame(1f, entering ? new System.Numerics.Vector3(0f, 0f, 0f) : new System.Numerics.Vector3((float)slideX, 0f, 0f), Avalonia.Animation.Easings.Easing.Parse(".65,0,.35,1.0"));
        offsetAnim.Duration = ShowHideAnimationDuration;
        offsetAnim.Target = nameof(visual.Offset);
        visual.StartAnimation(nameof(visual.Offset), offsetAnim);
    }

    /// <summary>
    /// 全天进度细轨：贴停靠缘的整高轨道。列表以等高槽位（课程格＋分隔槽）呈现一天，
    /// 槽位高度与课时长度无关，若按真实时间比例铺满首末跨度，课时不均匀时填充
    /// 会相对当前格错位出整格观感。故填充按槽位计：锚定第一个未结束的上课格，
    /// 锚点前的槽数＋节内时间占比为进度，课间冻结在边界处，放学走满；
    /// 无任何可渲染槽位时整根隐藏。由服务层定时器每周期调用。
    /// </summary>
    public void UpdateDayRail()
    {
        var plan = ScheduleList.ClassPlan;
        var layouts = plan?.TimeLayout?.Layouts;
        ICollection<TimeLayoutItem>? valid = plan?.ValidTimeLayoutItems;
        if (layouts == null || valid == null)
        {
            DayRail.IsVisible = false;
            return;
        }

        var now = ExactTimeService.GetCurrentLocalDateTime().TimeOfDay;
        var current = ScheduleList.CurrentTimeLayoutItem;

        // 锚点＝第一个未结束的可渲染上课格
        TimeLayoutItem? anchor = null;
        foreach (var item in layouts)
        {
            if (item.TimeType != 0) continue;
            if (!VerticalLessonsListBox.IsRenderable(item, valid, current)) continue;
            if (item.EndTime > now)
            {
                anchor = item;
                break;
            }
        }

        long anchorIndex = -1;
        long totalSlots = 0;
        foreach (var item in layouts)
        {
            if (!VerticalLessonsListBox.IsRenderable(item, valid, current)) continue;
            if (ReferenceEquals(item, anchor)) anchorIndex = totalSlots;
            totalSlots++;
        }

        if (totalSlots == 0)
        {
            DayRail.IsVisible = false;
            return;
        }

        // 早退分支会把轨藏起来；数据齐了必须显式点亮，否则一次过渡态就永久失明
        DayRail.IsVisible = true;

        double progress;
        if (anchor == null)
        {
            progress = 1;
        }
        else
        {
            var spanTicks = (anchor.EndTime - anchor.StartTime).Ticks;
            var fraction = spanTicks <= 0
                ? 1
                : Math.Clamp((now - anchor.StartTime).Ticks / (double)spanTicks, 0d, 1d);
            progress = Math.Clamp((anchorIndex + fraction) / totalSlots, 0d, 1d);
        }

        // 轨道高按内容层实际布局高显式计算（SizeToContent 下窗口 Height 属性不回写，
        // 布局量测滞后一帧）；轨道位于变换树外，直接用窗口逻辑坐标，并扣除圆弧避让
        // 内缩；轨道通体到端，超出圆角的部分由内容层圆角裁剪统一收掉
        var railHeight = ContentRoot.Bounds.Height - _dayRailVerticalInset * 2;
        if (railHeight > 0)
        {
            DayRailFill.Height = railHeight * progress;
        }
    }

    #region 上下课提醒状态机

    // 串行编舞链的六个演出阶段，按序推进；任何相邻阶段不并行播放
    private enum ReminderPhase
    {
        None,
        ScheduleFadeOut,
        PlateIn,
        Page,
        PageOut,
        PlateOut,
        ScheduleFadeIn,
        ScheduleDisplay,
    }

    private enum ReminderMode
    {
        None,
        Breaking,
        OnClass,
    }

    private ReminderPhase _reminderPhase = ReminderPhase.None;
    private ReminderMode _reminderMode = ReminderMode.None;
    private VerticalReminderOverlay.ReminderPage _currentPage;
    private int _reminderPageIndex;
    private int _reminderRoundsPlayed;
    private bool _onClassSilenced;
    private bool _pendingBreakingStart;
    private double _reminderPhaseElapsedMs;
    private long _reminderLastTick;

    // 编舞链各段时长（毫秒）；底板时长常量集中在覆盖层定义
    private const double ScheduleFadeMs = 200;
    private const double PageFadeMs = 200;

    private void LessonsServiceOnBreakingTime(object? sender, EventArgs e)
    {
        // 课间开始：重置静默与轮数并标记待启动；实际启动在 tick 内，窗口隐藏冻结期间不启动
        _onClassSilenced = false;
        _reminderRoundsPlayed = 0;
        _pendingBreakingStart = true;
    }

    /// <summary>
    /// 提醒状态机节拍：由服务层既有 50ms tick 调用，毫秒按真实时间差累计。
    /// 窗口规则性隐藏期间直接返回（计时冻结，恢复后续跑）；两功能全关时惰性旁路。
    /// </summary>
    public void UpdateReminder()
    {
        var vs = SettingsService.Settings.VerticalScheduleSettings;
        var anyEnabled = vs.IsBreakingReminderEnabled || vs.IsOnClassReminderEnabled;
        if (!anyEnabled && _reminderPhase == ReminderPhase.None)
        {
            return;
        }
        // 规则性隐藏期间计时冻结：持续刷新时间戳使恢复后的首个节拍从零起算
        if (!WindowRoot.IsVisible)
        {
            _reminderLastTick = Stopwatch.GetTimestamp();
            return;
        }

        var now = Stopwatch.GetTimestamp();
        if (_reminderLastTick != 0)
        {
            _reminderPhaseElapsedMs += (now - _reminderLastTick) * 1000.0 / Stopwatch.Frequency;
        }
        _reminderLastTick = now;

        // 运行中关闭两开关：页面立即隐藏，但仍按串行链走底板淡出→课表淡入收尾
        if (!anyEnabled && _reminderPhase is not (ReminderPhase.PlateOut or ReminderPhase.ScheduleFadeIn))
        {
            ReminderOverlay.AbortPages();
            BeginScheduleRestore();
        }

        switch (_reminderPhase)
        {
            case ReminderPhase.None:
                if (anyEnabled) TryStartReminder();
                break;
            case ReminderPhase.ScheduleFadeOut:
                if (_reminderPhaseElapsedMs >= ScheduleFadeMs)
                {
                    ScheduleList.IsVisible = false;
                    _reminderPhase = ReminderPhase.PlateIn;
                    _reminderPhaseElapsedMs = 0;
                    _ = ReminderOverlay.ShowPlateAsync();
                }
                break;
            case ReminderPhase.PlateIn:
                if (_reminderPhaseElapsedMs >= VerticalReminderOverlay.PlateFadeMs)
                {
                    EnterReminderPage(0);
                }
                break;
            case ReminderPhase.Page:
                if (_reminderPhaseElapsedMs >= GetCurrentPageDurationMs())
                {
                    _reminderPhase = ReminderPhase.PageOut;
                    _reminderPhaseElapsedMs = 0;
                    ReminderOverlay.FadeOutCurrentPage();
                }
                break;
            case ReminderPhase.PageOut:
                if (_reminderPhaseElapsedMs >= PageFadeMs)
                {
                    ReminderOverlay.CompletePageFadeOut();
                    // 各模式末拍索引：下课三拍（0-2），上课两拍（0-1）
                    var lastPageIndex = _reminderMode == ReminderMode.Breaking ? 2 : 1;
                    if (_reminderPageIndex < lastPageIndex)
                    {
                        EnterReminderPage(_reminderPageIndex + 1);
                    }
                    else
                    {
                        BeginScheduleRestore();
                    }
                }
                break;
            case ReminderPhase.PlateOut:
                if (_reminderPhaseElapsedMs >= VerticalReminderOverlay.PlateFadeMs)
                {
                    _reminderPhase = ReminderPhase.ScheduleFadeIn;
                    _reminderPhaseElapsedMs = 0;
                    RestoreSchedule();
                }
                break;
            case ReminderPhase.ScheduleFadeIn:
                if (_reminderPhaseElapsedMs >= ScheduleFadeMs)
                {
                    _reminderPhase = ReminderPhase.ScheduleDisplay;
                    _reminderPhaseElapsedMs = 0;
                }
                break;
            case ReminderPhase.ScheduleDisplay:
                if (_reminderPhaseElapsedMs >= GetCurrentScheduleDisplayMs())
                {
                    StartNextReminderRoundOrEnd();
                }
                break;
        }
    }

    private void TryStartReminder()
    {
        var vs = SettingsService.Settings.VerticalScheduleSettings;
        if (IsInOnClassWindow())
        {
            if (vs.IsOnClassReminderEnabled && !_onClassSilenced)
            {
                StartReminder(ReminderMode.OnClass);
                return;
            }
        }
        if (_pendingBreakingStart &&
            vs.IsBreakingReminderEnabled &&
            LessonsService.CurrentState == TimeState.Breaking)
        {
            StartReminder(ReminderMode.Breaking);
        }
    }

    /// <summary>
    /// 非上课态且距下节课 60 秒内（清晨第一节课前同样命中，属预期行为）。
    /// </summary>
    private bool IsInOnClassWindow()
    {
        var left = LessonsService.OnClassLeftTime;
        return LessonsService.CurrentState != TimeState.OnClass &&
               left > TimeSpan.Zero &&
               left <= TimeSpan.FromSeconds(60);
    }

    private void StartReminder(ReminderMode mode)
    {
        _pendingBreakingStart = false;
        _reminderMode = mode;
        _reminderRoundsPlayed = 0;
        BeginReminderRound();
    }

    private void BeginReminderRound()
    {
        _reminderPhase = ReminderPhase.ScheduleFadeOut;
        _reminderPhaseElapsedMs = 0;
        ScheduleList.Classes.Add("reminder-hidden");
        EmptyPlaceholder.Classes.Add("reminder-suppressed");
    }

    private void EnterReminderPage(int index)
    {
        _reminderPageIndex = index;
        // 下课提示三拍：「现在是下课时间」→「下一节课是」→ 下节课程全名；
        // 上课预告两拍：「即将上课」→ 课程全名
        _currentPage = _reminderMode == ReminderMode.Breaking
            ? index == 0
                ? VerticalReminderOverlay.ReminderPage.BreakingLabel
                : index == 1
                    ? VerticalReminderOverlay.ReminderPage.BreakingNextLessonLabel
                    : VerticalReminderOverlay.ReminderPage.BreakingNextLessonFullName
            : index == 0
                ? VerticalReminderOverlay.ReminderPage.OnClassIncoming
                : VerticalReminderOverlay.ReminderPage.OnClassFullName;
        ReminderOverlay.ShowPage(_currentPage);
        _reminderPhase = ReminderPhase.Page;
        _reminderPhaseElapsedMs = 0;
    }

    /// <summary>
    /// 收尾链起点：底板先淡出，完成后由状态机进入课表淡入；两段严格串行。
    /// </summary>
    private void BeginScheduleRestore()
    {
        _reminderRoundsPlayed++;
        _reminderPhase = ReminderPhase.PlateOut;
        _reminderPhaseElapsedMs = 0;
        _ = ReminderOverlay.HidePlateAsync();
    }

    /// <summary>
    /// 课表淡入：恢复列表可见性并撤下淡出类，透明度过渡承接淡入动画。
    /// </summary>
    private void RestoreSchedule()
    {
        ScheduleList.IsVisible = true;
        ScheduleList.Classes.Remove("reminder-hidden");
        EmptyPlaceholder.Classes.Remove("reminder-suppressed");
    }

    private void StartNextReminderRoundOrEnd()
    {
        var vs = SettingsService.Settings.VerticalScheduleSettings;
        var inWindow = IsInOnClassWindow();
        if (!vs.IsBreakingReminderEnabled && !vs.IsOnClassReminderEnabled)
        {
            EndReminder();
            return;
        }

        if (_reminderMode == ReminderMode.Breaking)
        {
            if (_reminderRoundsPlayed < vs.BreakingReminderMaxRounds &&
                !inWindow &&
                LessonsService.CurrentState == TimeState.Breaking)
            {
                BeginReminderRound();
                return;
            }
            if (vs.IsOnClassReminderEnabled && inWindow)
            {
                // 下课提示收尾时已进入临上课窗口，直接接力上课预告，不再开新的下课轮
                _reminderMode = ReminderMode.OnClass;
                _reminderRoundsPlayed = 0;
                BeginReminderRound();
                return;
            }
            EndReminder();
        }
        else
        {
            if (_reminderRoundsPlayed < vs.OnClassReminderMaxRounds && inWindow)
            {
                BeginReminderRound();
                return;
            }
            // 播满或离开窗口：静默至上课
            _onClassSilenced = true;
            EndReminder();
        }
    }

    private void EndReminder()
    {
        _reminderPhase = ReminderPhase.None;
        _reminderMode = ReminderMode.None;
        _reminderPhaseElapsedMs = 0;
    }

    /// <summary>
    /// 上课铃打断时立即中止提醒并还原课表。此处是编舞链唯一允许并行的位置：
    /// 底板淡出与课表淡入同时进行以尽快归位，终态（课表完整可见）不受影响。
    /// </summary>
    private void InterruptReminder()
    {
        if (_reminderPhase != ReminderPhase.None)
        {
            ReminderOverlay.AbortPages();
            _ = ReminderOverlay.HidePlateAsync();
            RestoreSchedule();
        }
        _onClassSilenced = false;
        _reminderRoundsPlayed = 0;
        _pendingBreakingStart = false;
        EndReminder();
    }

    private double GetCurrentPageDurationMs()
    {
        var vs = SettingsService.Settings.VerticalScheduleSettings;
        if (_reminderMode == ReminderMode.Breaking)
        {
            return (_reminderPageIndex == 0
                ? vs.BreakingReminderBreakLabelSeconds
                : vs.BreakingReminderNextLessonSeconds) * 1000.0;
        }
        return vs.OnClassReminderPageSeconds * 1000.0;
    }

    private double GetCurrentScheduleDisplayMs()
    {
        var vs = SettingsService.Settings.VerticalScheduleSettings;
        return (_reminderMode == ReminderMode.Breaking
            ? vs.BreakingReminderScheduleDisplaySeconds
            : vs.OnClassReminderScheduleDisplaySeconds) * 1000.0;
    }

    #endregion

    /// <summary>
    /// 窗口层级：完全跟随横条，Avalonia Topmost＋原生 SetWindowPos 双保险（主窗 UpdateWindowLayer 简化版）。
    /// </summary>
    public void UpdateWindowLayer()
    {
        switch (SettingsService.Settings.WindowLayer)
        {
            case 0:
                Topmost = false;
                PlatformServices.WindowPlatformService.SetWindowFeature(this, WindowFeatures.Bottommost, true);
                break;
            case 1:
                Topmost = true;
                // 防止切换层级时残留 Bottommost 状态（同主窗做法）
                PlatformServices.WindowPlatformService.SetWindowFeature(this, WindowFeatures.Bottommost, false);
                PlatformServices.WindowPlatformService.SetWindowFeature(this, WindowFeatures.Topmost, true);
                break;
        }
    }

    /// <summary>
    /// 点击穿透 v1 常开；录制/防截图跟随横条（主窗 UpdateWindowFeatures 同款）。
    /// </summary>
    public void UpdateWindowFeatures()
    {
        var settings = SettingsService.Settings;
        PlatformServices.WindowPlatformService.SetWindowFeature(this, WindowFeatures.Transparent, true);
        // 录制模式开启时抑制 ToolWindow 样式，与主窗一致
        PlatformServices.WindowPlatformService.SetWindowFeature(this, WindowFeatures.ToolWindow,
            !settings.IsScreenRecordingModeEnabled);
        // Private 即 WDA_EXCLUDEFROMCAPTURE 防截图
        PlatformServices.WindowPlatformService.SetWindowFeature(this, WindowFeatures.Private,
            settings.IsWindowCaptureBlockingEnabled);
    }

    /// <summary>
    /// 文字规格与观感度量本地注入：主题系统把字号写在主窗窗口作用域，竖窗解析不到，
    /// 故在初始化及设置变更时把字号（含全局缩放）、格高与各装饰元素尺寸写入本地资源，
    /// 模板经 DynamicResource 命中。
    /// </summary>
    private void UpdateTypographyResources()
    {
        var settings = SettingsService.Settings;
        // 横条同款做法：内容树整体布局变换承担缩放，本地注入的资源一律保持原始值；
        // 赋值走等值守卫，拖动缩放时避免同值重写触发全树 DynamicResource 失效重排
        var scale = GetContentScale();
        if (ContentScaleHost.LayoutTransform is ScaleTransform contentScale)
        {
            contentScale.ScaleX = scale;
            contentScale.ScaleY = scale;
        }
        SetResourceValue("MainWindowEmphasizedFontSize", settings.MainWindowEmphasizedFontSize);
        SetResourceValue("MainWindowBodyFontSize", settings.MainWindowBodyFontSize);
        SetResourceValue("MainWindowLargeFontSize", settings.MainWindowLargeFontSize);
        // 上课预告竖排每字字号：默认同大号档，提醒显示期间按可用高度自动缩级
        SetResourceValue("VerticalReminderVerticalFontSize", settings.MainWindowLargeFontSize);
        SetResourceValue("VerticalScheduleCellHeight", CellHeight);
        SetResourceValue("VerticalSchedulePlaceholderIconSize", 28d);
        // 端部留白：首末可渲染格避让胶囊圆角裁剪区
        SetResourceValue("VerticalScheduleEdgePaddingTop", new Thickness(0, 8, 0, 0));
        SetResourceValue("VerticalScheduleEdgePaddingBottom", new Thickness(0, 0, 0, 8));
        // 全天进度细轨位于变换树外，按窗口坐标给宽：原始宽 3 乘生效缩放
        SetResourceValue("VerticalScheduleDayRailWidthScaled", 3d * scale);
        // 变换树内内容宽度钉住截面宽原始值，供 SizeToContent 定窗
        SetResourceValue("VerticalScheduleBaseWidth", settings.VerticalScheduleSettings.Thickness);
        UpdateDayRailGeometry();
        UpdateDayRailDock();
        UpdateSeparatorTickLength();
    }

    /// <summary>
    /// 等值守卫的资源注入：同值跳过，杜绝拖动缩放等连续变更场景下的无效失效重排。
    /// </summary>
    private void SetResourceValue(string key, object value)
    {
        if (!Equals(Resources[key], value))
        {
            Resources[key] = value;
        }
    }

    /// <summary>
    /// 按停靠方向放置全天进度细轨：左缘停靠贴条内左缘，右缘停靠贴右缘；
    /// <summary>
    /// 按停靠方向放置全天进度细轨：轨道横向贴死停靠缘（无水平内缩）、纵向无避让，
    /// 端部超出圆角的部分由内容层圆角裁剪统一收掉。初始化与停靠/缩放变更时调用。
    /// </summary>
    private void UpdateDayRailDock()
    {
        var vsSettings = SettingsService.Settings.VerticalScheduleSettings;
        DayRail.Margin = new Thickness(0);
        DayRail.HorizontalAlignment = vsSettings.DockingEdge == 0
            ? Avalonia.Layout.HorizontalAlignment.Left
            : Avalonia.Layout.HorizontalAlignment.Right;
    }

    /// <summary>
    /// 上课预告竖排页字号缩级：画面所需高超出工作区可用高时按比例压缩字号，
    /// 等比缩放等价于逐级缩字号；结果注入本地资源并同步到已构建的字块。
    /// </summary>
    private void UpdateVerticalReminderFont(double availableHeight)
    {
        var settings = SettingsService.Settings;
        var baseFont = settings.MainWindowLargeFontSize;
        var font = baseFont;
        // 估算值为变换树内原始高度，与窗口逻辑可用高比较前乘生效缩放
        var estimate = ReminderOverlay.EstimateRequiredHeight(VerticalReminderOverlay.ReminderPage.OnClassFullName)
                       * GetContentScale();
        if (estimate > availableHeight && estimate > 0)
        {
            font = baseFont * Math.Clamp(availableHeight / estimate, 0.1, 1.0);
        }

        var current = Resources["VerticalReminderVerticalFontSize"] is double d ? d : baseFont;
        if (Math.Abs(current - font) > 0.01)
        {
            Resources["VerticalReminderVerticalFontSize"] = font;
            ReminderOverlay.ApplyVerticalFontSize(font);
        }
    }

    /// <summary>
    /// 注入分隔刻度长度＝竖条宽度的 62.5%（含全局缩放与竖条缩放）。初始化、缩放与
    /// 竖条宽度变更时调用，分隔模板经 DynamicResource 自动跟随。
    /// </summary>
    private void UpdateSeparatorTickLength()
    {
        var settings = SettingsService.Settings;
        Resources["VerticalScheduleSeparatorTickLength"] = 0.625 * settings.VerticalScheduleSettings.Thickness;
    }

    /// <summary>
    /// 注入竖条生效圆角：独立圆角开启时取独立值，否则跟随全局 RadiusX。
    /// 胶囊着色层、内容层裁剪与提醒底板三处共用同一资源，保证形状一致。
    /// </summary>
    private void UpdateCornerRadiusResource()
    {
        var radius = GetEffectiveCornerRadius();
        SetResourceValue("VerticalScheduleCornerRadius", new CornerRadius(radius));
        // 变换树内的圆角（提醒底板）随整体缩放放大，才能与外层未变换胶囊的裁剪形状贴合
        var scaled = radius * GetContentScale();
        SetResourceValue("VerticalScheduleCornerRadiusScaled", new CornerRadius(scaled));
        UpdateDayRailGeometry();
    }

    private double GetEffectiveCornerRadius()
    {
        var vsSettings = SettingsService.Settings.VerticalScheduleSettings;
        return vsSettings.IsCustomCornerRadiusEnabled
            ? vsSettings.CustomCornerRadius
            : SettingsService.Settings.RadiusX;
    }

    /// <summary>
    /// 内容树整体布局变换的生效缩放＝全局界面缩放 × 竖条界面缩放。
    /// </summary>
    private double GetContentScale()
    {
        var settings = SettingsService.Settings;
        return settings.Scale * settings.VerticalScheduleSettings.Zoom;
    }

    // 进度轨纵向内缩量：顶端/底端恰好从胶囊圆弧与轨外侧边的交点处起步
    private double _dayRailVerticalInset;

    /// <summary>
    /// 进度轨圆角避让：轨全高贴边时，其外侧边会探入胶囊圆弧区域，特定圆角值下
    /// 顶端露出一段弧外像素。按圆弧几何计算纵向内缩，使轨端从弧线处精确起步。
    /// </summary>
    private void UpdateDayRailGeometry()
    {
        var radius = GetEffectiveCornerRadius();
        var railWidth = 3d * GetContentScale();
        double inset;
        if (radius <= 0 || railWidth <= 0)
        {
            inset = 0;
        }
        else if (railWidth >= radius)
        {
            inset = radius;
        }
        else
        {
            inset = radius - Math.Sqrt(radius * radius - (radius - railWidth) * (radius - railWidth));
        }

        inset = Math.Clamp(inset, 0, Height / 4);
        _dayRailVerticalInset = inset;
        DayRail.Margin = new Thickness(0, inset, 0, inset);
    }

    /// <summary>
    /// 前景色本地注入：与主窗同款机制，跟随主界面字体颜色设置。
    /// </summary>
    private void UpdateForegroundColor()
    {
        var settings = SettingsService.Settings;
        ControlColorHelper.SetControlForegroundColor(ContentRoot, settings.CustomForegroundColor,
            settings.IsCustomForegroundColorEnabled);
    }

    public override void Show()
    {
        ShowActivated = false;
        ShowInTaskbar = false;
        // 先完成一次定位计算再显示，防冷启动闪现
        UpdateWindowPos();
        UpdateVisibility();
        base.Show();
        UpdateWindowLayer();
        UpdateWindowFeatures();
        // 窗口整体显示视为一次显隐翻转，播放入场动画
        PlayShowHideAnimation(entering: true);
    }

    private void RulesetServiceOnStatusUpdated(object? sender, EventArgs e)
    {
        // 全屏/最大化判定（主窗 RulesetServiceOnStatusUpdated 同款）
        var screen = GetSelectedScreenSafe();
        if (screen == null) return;
        IsForegroundFullscreen = PlatformServices.WindowPlatformService.IsForegroundWindowFullscreen(screen);
        IsForegroundMaxWindow = PlatformServices.WindowPlatformService.IsForegroundWindowMaximized(screen);
        UpdateVisibility();
    }

    private void LessonsServiceOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LessonsService.CurrentState)) return;
        if (LessonsService.CurrentState == TimeState.OnClass)
        {
            // 上课铃打断：提醒立即让位，课表完整还原
            InterruptReminder();
        }
        UpdateVisibility();
    }

    private void SettingsOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SettingsService.Settings.IsMainWindowVisible):
            case nameof(SettingsService.Settings.HideOnClass):
            case nameof(SettingsService.Settings.HideOnFullscreen):
            case nameof(SettingsService.Settings.HideOnMaxWindow):
                UpdateVisibility();
                break;
            case nameof(SettingsService.Settings.MainWindowEmphasizedFontSize):
            case nameof(SettingsService.Settings.MainWindowBodyFontSize):
            case nameof(SettingsService.Settings.Scale):
                UpdateTypographyResources();
                UpdateCornerRadiusResource();
                // 先同步落定 SizeToContent 尺寸再定位，位置与尺寸同一调度轮回，
                // 避免交换链把旧帧拉伸到新窗口矩形（上一帧闪烁）
                ContentRoot.UpdateLayout();
                UpdateWindowPos();
                UpdateDayRail();
                break;
            case nameof(SettingsService.Settings.CustomForegroundColor):
            case nameof(SettingsService.Settings.IsCustomForegroundColorEnabled):
                UpdateForegroundColor();
                break;
            case nameof(SettingsService.Settings.RadiusX):
                UpdateCornerRadiusResource();
                UpdateDayRail();
                break;
            case nameof(SettingsService.Settings.VerticalScheduleSettings):
                // 嵌套设置的子属性变更统一以容器属性名转发到达；此处响应停靠方向、
                // 竖条宽度、整体缩放与独立圆角变化（缩放影响全部注入资源，走全量刷新）
                UpdateTypographyResources();
                UpdateCornerRadiusResource();
                ContentRoot.UpdateLayout();
                UpdateWindowPos();
                UpdateDayRail();
                break;
        }
    }

    private void VerticalScheduleWindow_OnActivated(object? sender, EventArgs e)
    {
        // 激活时重断言穿透与层级（主窗同款防丢做法）
        UpdateWindowFeatures();
        UpdateWindowLayer();
    }

    private void VerticalScheduleWindow_OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (e.CloseReason is WindowCloseReason.OSShutdown or WindowCloseReason.ApplicationShutdown)
        {
            return;
        }
        e.Cancel = true;
    }
}

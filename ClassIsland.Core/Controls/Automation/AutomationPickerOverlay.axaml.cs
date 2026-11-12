using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Commands;
using ClassIsland.Core.Icons;
using ClassIsland.Core.Models.Automation;
using FluentAvalonia.UI.Controls;
using Va = Avalonia.Layout.VerticalAlignment;
using Ha = Avalonia.Layout.HorizontalAlignment;
using Or = Avalonia.Layout.Orientation;

namespace ClassIsland.Core.Controls.Automation;

/// <summary>
/// 自动化选择面板。覆盖宿主容器，以“返回 + 搜索 + 分类栏 + 条目列表 + 模式切换”的形式
/// 替换旧的触发器下拉框与行动级联菜单。
/// 挂载后自注册为 <see cref="Current"/>，调用方经 <see cref="AutomationPickerOverlayExtensions.ExecutePick"/>
/// 打开面板；条目来源为触发器注册表与行动菜单树，第三方插件注册的条目自动进入面板。
/// 非定向请求（未带 <see cref="AutomationPickRequest.Callback"/>）的落地走 <see cref="Delivery"/>，
/// 因此面板内切换触发器/行动模式后，选中项始终落在当前模式对应的目标上。
/// </summary>
public partial class AutomationPickerOverlay : UserControl
{
    private static readonly string[] PreferredCategoryOrder =
    [
        "课程与时间", "应用生命周期", "事件与集成",
        "提醒与显示", "应用与系统", "流程控制"
    ];

    private static readonly string DefaultIconExpression = FluentIcons.PersonWalkingRegular;

    /// <summary>
    /// 当前挂载的面板实例。ExecutePick 经它打开面板；多个宿主同时挂载时以后挂载者为准。
    /// </summary>
    public static AutomationPickerOverlay? Current { get; private set; }

    private AutomationPickRequest? _request;
    private AutomationPickerKind _mode = AutomationPickerKind.Trigger;
    private string? _category;          // null = 全部
    private bool _isSearchView;
    private string _query = "";
    private int _cursor;
    private List<AutomationPickerItem> _flat = [];
    private List<Button> _rowRefs = [];
    private bool _animating;

    // ----- 面板视图选项（会话级） -----
    private bool _showExistingMarks = true;
    private bool _showHintId = true;
    private bool _compact;

    /// <summary>
    /// 宿主投递：非定向请求选中一项后按当前模式落地。由宿主页面在挂载时设置。
    /// </summary>
    public Action<AutomationPickerKind, AutomationPickerItem>? Delivery { get; set; }

    /// <summary>
    /// 宿主提供各模式下已存在 ID 集合，用于“已添加”标记。
    /// </summary>
    public Func<AutomationPickerKind, IEnumerable<string>>? ExistingIdsProvider { get; set; }

    /// <summary>
    /// 是否回退经典选择方式（行内下拉与级联菜单）。由宿主页面绑定到持久化设置。
    /// </summary>
    public static readonly StyledProperty<bool> UseClassicMenuProperty =
        AvaloniaProperty.Register<AutomationPickerOverlay, bool>(nameof(UseClassicMenu));

    public bool UseClassicMenu
    {
        get => GetValue(UseClassicMenuProperty);
        set => SetValue(UseClassicMenuProperty, value);
    }

    /// <summary>
    /// 经典菜单开关变化，宿主据此写入持久化设置并刷新各选择入口。
    /// </summary>
    public event EventHandler? ClassicMenuChanged;

    public AutomationPickerOverlay()
    {
        InitializeComponent();
        Root.IsVisible = false;
        Root.AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        ListHost.PointerExited += (_, _) => ShowDefaultHint();
        SearchTextBox.TextChanged += SearchTextBox_OnTextChanged;
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Current = this;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (Current == this)
        {
            Current = null;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == UseClassicMenuProperty)
        {
            ClassicMenuChanged?.Invoke(this, e);
        }
    }

    /// <summary>
    /// 打开面板。同一时间只处理一次请求，重复打开以最后一次为准。
    /// </summary>
    public void Open(AutomationPickRequest request)
    {
        _request = request;
        _mode = request.Kind;
        _category = null;
        _isSearchView = false;
        _query = "";
        _cursor = 0;
        _animating = false;
        ListWrap.Classes.Remove("out-l");
        ListWrap.Classes.Remove("out-r");
        ListWrap.Classes.Remove("in-l");
        ListWrap.Classes.Remove("in-r");

        // 换类型等定向场景锁定模式，右栏按钮不可用。
        var locked = request.LockMode;
        TriggerModeButton.IsEnabled = !locked;
        ActionModeButton.IsEnabled = !locked;

        SearchTextBox.Text = "";
        UpdateSearchWatermark();
        UpdateModeButtons();
        RebuildAll();

        Root.IsVisible = true;
        Root.Classes.Add("open");
        SearchTextBox.Focus();
    }

    /// <summary>
    /// 关闭面板并播收回退动画。
    /// </summary>
    public void Close()
    {
        Root.Classes.Remove("open");
        DispatcherTimer.RunOnce(() => Root.IsVisible = false, TimeSpan.FromMilliseconds(260));
    }

    private void Back_OnClick(object sender, RoutedEventArgs e) => Close();

    private void TriggerMode_OnClick(object sender, RoutedEventArgs e) => SwitchMode(AutomationPickerKind.Trigger);

    private void ActionMode_OnClick(object sender, RoutedEventArgs e) => SwitchMode(AutomationPickerKind.Action);

    // ---------- 数据构建 ----------

    private static List<AutomationPickerItem> BuildTriggerItems()
    {
        return IAutomationService.RegisteredTriggers
            .Select(t => new AutomationPickerItem
            {
                Id = t.Id,
                Name = t.Name,
                IconSource = t.IconSource,
                Category = NormalizeCategory(t.Category)
            })
            .ToList();
    }

    private static List<AutomationPickerItem> BuildActionItems()
    {
        var result = new List<AutomationPickerItem>();
        // 拍平菜单树而非直接枚举 ActionInfos：菜单树携带同 ID 多预设变体与插件挂载的条目。
        foreach (var node in IActionService.IListActionMenuTree)
        {
            CollectMenuNode(result, node);
        }
        return result;
    }

    private static void CollectMenuNode(List<AutomationPickerItem> result, ActionMenuTreeNode node)
    {
        switch (node)
        {
            case ActionMenuTreeGroup group:
                foreach (var child in group.Children)
                {
                    CollectMenuNode(result, child);
                }
                break;
            case ActionMenuTreeItem item:
            {
                IActionService.ActionInfos.TryGetValue(item.ActionItemId, out var info);
                result.Add(new AutomationPickerItem
                {
                    Id = item.ActionItemId,
                    Name = item.Name,
                    IconSource = item.IconSource ?? info?.IconSource,
                    Category = NormalizeCategory(info?.Category),
                    MenuSource = item
                });
                break;
            }
        }
    }

    private static string NormalizeCategory(string? category) =>
        string.IsNullOrWhiteSpace(category) ? "未分类" : category;

    private string ModeLabel => _mode == AutomationPickerKind.Trigger ? "触发器" : "行动";

    private IEnumerable<AutomationPickerItem> SourceItems =>
        _mode == AutomationPickerKind.Trigger ? BuildTriggerItems() : BuildActionItems();

    private IEnumerable<string> GetExistingIds() =>
        _request?.ExistingIdsProvider?.Invoke()
        ?? ExistingIdsProvider?.Invoke(_mode)
        ?? [];

    /// <summary>
    /// 分类排序：内置语义分类按既定顺序在前，其余（插件分类、回退分类）按出现顺序在后。
    /// </summary>
    private List<string> BuildCategories(List<AutomationPickerItem> items)
    {
        var seen = items.Select(x => x.Category).Distinct().ToList();
        var result = PreferredCategoryOrder.Where(seen.Contains).ToList();
        result.AddRange(seen.Except(result));
        return result;
    }

    // ---------- 视图渲染 ----------

    private void RebuildAll()
    {
        RebuildCategories();
        RebuildList();
        ShowDefaultHint();
    }

    private void RebuildCategories()
    {
        CatsHost.Children.Clear();
        var items = SourceItems.ToList();

        AddCategoryButton(null, ModeLabel == "触发器" ? "全部触发器" : "全部行动", items.Count);
        foreach (var cat in BuildCategories(items))
        {
            AddCategoryButton(cat, cat, items.Count(x => x.Category == cat));
        }
    }

    private void AddCategoryButton(string? category, string name, int count)
    {
        var isOn = _category == category;
        // 指示条独占第 0 列并带固定右距，避免与分类名挤在同一格（曾出现导轨压住「全部行动」文字）。
        var bar = new Border
        {
            Width = 3,
            Height = 16,
            CornerRadius = new CornerRadius(1.5),
            Background = isOn ? GetBrush("AccentFillColorDefaultBrush") : Brushes.Transparent,
            VerticalAlignment = Va.Center
        };

        var text = new TextBlock { Text = name, VerticalAlignment = Va.Center };
        var countText = new TextBlock
        {
            Text = count.ToString(),
            VerticalAlignment = Va.Center,
            FontSize = 12,
            // 右侧留出一整段滚动条宽度：分类一多导轨出现滚动条时，悬停浮层不会再盖到计数上。
            Margin = new Thickness(8, 0, 12, 0)
        };
        ApplyForeground(countText, TextBlock.ForegroundProperty, "TextFillColorTertiaryBrush");

        var button = new Button
        {
            Classes = { "picker-cat" },
            Tag = category,
            HorizontalAlignment = Ha.Stretch,
            HorizontalContentAlignment = Ha.Stretch,
            BorderThickness = new Thickness(0)
        };
        if (isOn)
        {
            button.Classes.Add("on");
        }
        button.Content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,12,*,Auto"),
            Children = { bar, text, countText }
        };
        Grid.SetColumn(text, 2);
        Grid.SetColumn(countText, 3);
        button.Click += (_, _) =>
        {
            _category = category;
            _isSearchView = false;
            _query = "";
            SearchTextBox.Text = "";
            _cursor = 0;
            RebuildCategories();
            RebuildList();
            ShowDefaultHint();
        };
        CatsHost.Children.Add(button);
    }

    private void RebuildList()
    {
        ListHost.Children.Clear();
        _flat = [];
        _rowRefs = [];

        if (_isSearchView)
        {
            BuildSearchView();
        }
        else
        {
            BuildDirectoryView();
        }

        _cursor = Math.Min(_cursor, Math.Max(0, _flat.Count - 1));
        PaintCursor(scrollToCursor: false);
    }

    private void BuildDirectoryView()
    {
        var items = SourceItems.Where(x => _category == null || x.Category == _category).ToList();
        foreach (var group in GroupByCategory(items))
        {
            AddGroupHeader(group.Category, group.Items.Count);
            AddRows(group.Items);
        }
    }

    private void BuildSearchView()
    {
        var scored = SourceItems
            .Select(x => (Item: x, Score: ScoreItem(x)))
            .Where(x => x.Score > 0)
            .ToList();

        var meta = new TextBlock
        {
            Text = $"{scored.Count} 个结果 · {ModeLabel}",
            FontSize = 12,
            Margin = new Thickness(8, 6, 0, 8)
        };
        ApplyForeground(meta, TextBlock.ForegroundProperty, "TextFillColorTertiaryBrush");
        ListHost.Children.Add(meta);

        if (scored.Count == 0)
        {
            BuildEmptyState();
            return;
        }

        var best = scored.Aggregate((a, b) => b.Score > a.Score ? b : a).Item;
        AddGroupHeader("最佳匹配", null, accent: true);
        AddRow(best);

        var rest = scored.Where(x => !ReferenceEquals(x.Item, best)).Select(x => x.Item).ToList();
        if (rest.Count == 0)
        {
            return;
        }
        AddGroupHeader("其他结果", rest.Count);
        // 组顺序在输入过程中保持稳定：按既定分类顺序遍历，不在命中时重排。
        foreach (var group in GroupByCategory(rest))
        {
            AddGroupHeader(group.Category, group.Items.Count, indent: true);
            AddRows(group.Items);
        }
    }

    private int ScoreItem(AutomationPickerItem item)
    {
        var query = _query.Trim();
        if (query.Length == 0)
        {
            return 0;
        }
        if (item.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }
        if (item.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }
        return item.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    private IEnumerable<(string Category, List<AutomationPickerItem> Items)> GroupByCategory(
        List<AutomationPickerItem> items)
    {
        if (_category != null)
        {
            yield return (_category, items);
            yield break;
        }
        foreach (var cat in BuildCategories(items))
        {
            var group = items.Where(x => x.Category == cat).ToList();
            if (group.Count > 0)
            {
                yield return (cat, group);
            }
        }
    }

    private void AddGroupHeader(string title, int? count, bool accent = false, bool indent = false)
    {
        var header = new StackPanel
        {
            Orientation = Or.Horizontal,
            Margin = new Thickness(indent ? 26 : 8, 12, 0, 4)
        };
        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 12,
            FontWeight = FontWeight.Medium,
            VerticalAlignment = Va.Center
        };
        ApplyForeground(titleText, TextBlock.ForegroundProperty, accent ? "AccentTextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush");
        header.Children.Add(titleText);
        if (count.HasValue)
        {
            var countText = new TextBlock
            {
                Text = count.Value.ToString(),
                FontSize = 12,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = Va.Center
            };
            ApplyForeground(countText, TextBlock.ForegroundProperty, "TextFillColorTertiaryBrush");
            header.Children.Add(countText);
        }
        ListHost.Children.Add(header);
    }

    private void AddRows(List<AutomationPickerItem> items)
    {
        foreach (var item in items)
        {
            AddRow(item);
        }
    }

    private void AddRow(AutomationPickerItem item)
    {
        var existing = _showExistingMarks && GetExistingIds().Contains(item.Id);

        var bar = new Border
        {
            Width = 3,
            Height = 20,
            CornerRadius = new CornerRadius(1.5),
            Background = Brushes.Transparent,
            Margin = new Thickness(8, 0, 0, 0),
            Opacity = 0,
            Transitions = new Transitions
            {
                new DoubleTransition { Duration = TimeSpan.FromMilliseconds(150), Property = OpacityProperty }
            }
        };
        var icon = new IconText
        {
            IconSource = item.IconSource,
            Glyph = DefaultIconExpression,
            Margin = new Thickness(12, 0, 0, 0)
        };
        var name = new TextBlock
        {
            Text = item.Name,
            VerticalAlignment = Va.Center,
            Margin = new Thickness(12, 0, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var badge = new StackPanel
        {
            Orientation = Or.Horizontal,
            Spacing = 4,
            Margin = new Thickness(0, 0, 16, 0),
            IsVisible = existing,
            VerticalAlignment = Va.Center
        };
        var badgeIcon = new IconText { Glyph = FluentIcons.CheckmarkRegular };
        ApplyForeground(badgeIcon, IconText.ForegroundProperty, "AccentTextFillColorPrimaryBrush");
        badge.Children.Add(badgeIcon);
        var badgeText = new TextBlock
        {
            Text = "已添加",
            FontSize = 12,
            VerticalAlignment = Va.Center
        };
        ApplyForeground(badgeText, TextBlock.ForegroundProperty, "AccentTextFillColorPrimaryBrush");
        badge.Children.Add(badgeText);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"),
            Children = { bar, icon, name, badge }
        };
        Grid.SetColumn(icon, 1);
        Grid.SetColumn(name, 2);
        Grid.SetColumn(badge, 3);

        var row = new Button
        {
            Classes = { "picker-row" },
            Content = grid,
            HorizontalAlignment = Ha.Stretch,
            HorizontalContentAlignment = Ha.Stretch,
            BorderThickness = new Thickness(0),
            MinHeight = _compact ? 34 : 44,
            Margin = new Thickness(0, 0, 0, 2)
        };
        row.Click += (_, _) => ActivateItem(item);
        row.PointerEntered += (_, _) => ShowItemHint(item);

        _flat.Add(item);
        _rowRefs.Add(row);
        ListHost.Children.Add(row);
    }

    private void BuildEmptyState()
    {
        var panel = new StackPanel
        {
            HorizontalAlignment = Ha.Center,
            Margin = new Thickness(0, 64, 0, 0)
        };
        var icon = new IconText
        {
            Glyph = FluentIcons.SearchRegular,
            FontSize = 28,
            HorizontalAlignment = Ha.Center
        };
        ApplyForeground(icon, IconText.ForegroundProperty, "TextFillColorTertiaryBrush");
        panel.Children.Add(icon);
        var title = new TextBlock
        {
            Text = $"没有找到「{_query}」",
            FontSize = 14,
            Margin = new Thickness(0, 12, 0, 0),
            HorizontalAlignment = Ha.Center
        };
        ApplyForeground(title, TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        panel.Children.Add(title);
        var sub = new TextBlock
        {
            Text = "试试更短的关键词，或按 Esc 返回目录",
            FontSize = 12,
            Margin = new Thickness(0, 5, 0, 0),
            HorizontalAlignment = Ha.Center
        };
        ApplyForeground(sub, TextBlock.ForegroundProperty, "TextFillColorTertiaryBrush");
        panel.Children.Add(sub);
        ListHost.Children.Add(panel);
    }

    // ---------- 光标与提示栏 ----------

    private void PaintCursor(bool scrollToCursor)
    {
        for (var i = 0; i < _rowRefs.Count; i++)
        {
            var isCursor = i == _cursor;
            SetClass(_rowRefs[i].Classes, "cursor", isCursor);
            var bar = FindBar(_rowRefs[i]);
            if (bar != null)
            {
                bar.Opacity = isCursor ? 1 : 0;
                bar.Background = GetBrush("AccentFillColorDefaultBrush");
            }
        }

        if (scrollToCursor && _cursor >= 0 && _cursor < _rowRefs.Count)
        {
            _rowRefs[_cursor].BringIntoView();
        }
    }

    private static Border? FindBar(Button row)
    {
        if (row.Content is Grid grid)
        {
            foreach (var child in grid.Children)
            {
                if (child is Border { Width: 3 } bar)
                {
                    return bar;
                }
            }
        }
        return null;
    }

    private void MoveCursor(int delta)
    {
        if (_flat.Count == 0)
        {
            return;
        }
        _cursor = (_cursor + delta + _flat.Count) % _flat.Count;
        PaintCursor(scrollToCursor: true);
        ShowItemHint(_flat[_cursor]);
    }

    private void ShowDefaultHint()
    {
        // 无悬停条目时提示栏留空，不输出操作说明。
        HintHost.Children.Clear();
    }

    private void ShowItemHint(AutomationPickerItem item)
    {
        HintHost.Children.Clear();
        var panel = new StackPanel
        {
            Orientation = Or.Horizontal,
            Spacing = 10
        };
        panel.Children.Add(new IconText
        {
            IconSource = item.IconSource,
            Glyph = DefaultIconExpression
        });
        panel.Children.Add(new TextBlock
        {
            Text = item.Name,
            FontSize = 13,
            FontWeight = FontWeight.Medium,
            VerticalAlignment = Va.Center
        });
        if (_showHintId)
        {
            var id = new TextBlock
            {
                Text = item.Id,
                FontSize = 11,
                FontFamily = new FontFamily("Consolas"),
                VerticalAlignment = Va.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            ApplyForeground(id, TextBlock.ForegroundProperty, "TextFillColorTertiaryBrush");
            panel.Children.Add(id);
        }
        HintHost.Children.Add(panel);
    }

    // ---------- 选择与模式切换 ----------

    private void ActivateItem(AutomationPickerItem item)
    {
        var request = _request;
        Close();
        if (request?.Callback != null)
        {
            // 定向请求（换类型等）：无论面板当前处于哪个模式，都交回请求方。
            request.Callback(item);
            return;
        }
        // 非定向请求：面板内切到哪个模式，选中项就落到哪个目标。
        Delivery?.Invoke(_mode, item);
    }

    private void SwitchMode(AutomationPickerKind mode)
    {
        if (mode == _mode || _animating || _request?.LockMode == true)
        {
            return;
        }

        // 记录本次点中的按钮内容，UpdateModeButtons 里播落定脉冲。
        _pulseTarget = (mode == AutomationPickerKind.Trigger
            ? TriggerModeButton.Content
            : ActionModeButton.Content) as StackPanel;
        _animating = true;
        var leaving = mode == AutomationPickerKind.Action ? "out-l" : "out-r";
        var entering = mode == AutomationPickerKind.Action ? "in-r" : "in-l";
        ListWrap.Classes.Add(leaving);
        DispatcherTimer.RunOnce(() =>
        {
            _mode = mode;
            _category = null;
            _isSearchView = false;
            _query = "";
            _cursor = 0;
            SearchTextBox.Text = "";
            UpdateSearchWatermark();
            UpdateModeButtons();
            RebuildAll();
            ListWrap.Classes.Remove(leaving);
            ListWrap.Classes.Add(entering);
            DispatcherTimer.RunOnce(() =>
            {
                ListWrap.Classes.Remove(entering);
                _animating = false;
            }, TimeSpan.FromMilliseconds(200));
        }, TimeSpan.FromMilliseconds(150));
    }

    private void UpdateModeButtons()
    {
        SetClass(TriggerModeButton.Classes, "on", _mode == AutomationPickerKind.Trigger);
        SetClass(ActionModeButton.Classes, "on", _mode == AutomationPickerKind.Action);
        if (_pulseTarget != null)
        {
            // 落定脉冲：挂 pulse 让内容放大到过冲位，短暂停留后摘掉，transition 自动弹回基态。
            // 摘除延后到过渡播完（250ms），期间悬停态由 :pointerover 样式接管不冲突。
            var body = _pulseTarget;
            _pulseTarget = null;
            body.Classes.Remove("pulse");
            Dispatcher.UIThread.Post(() =>
            {
                body.Classes.Add("pulse");
                DispatcherTimer.RunOnce(() => body.Classes.Remove("pulse"), TimeSpan.FromMilliseconds(220));
            }, DispatcherPriority.Background);
        }
    }

    /// <summary>最近一次切换的模式按钮内容，供落定脉冲定位；null 表示本次不播。</summary>
    StackPanel? _pulseTarget;

    private void UpdateSearchWatermark()
    {
        SearchTextBox.Watermark = _mode == AutomationPickerKind.Trigger ? "搜索触发器…" : "搜索行动…";
    }

    private static void SetClass(Classes classes, string name, bool on)
    {
        if (on)
        {
            classes.Add(name);
        }
        else
        {
            classes.Remove(name);
        }
    }

    // ---------- 头部菜单 ----------

    private void ToggleClassic_OnClick(object? sender, RoutedEventArgs e)
    {
        // 面板/经典菜单的切换持久化由宿主页面负责，这里只翻转并收起面板。
        UseClassicMenu = !UseClassicMenu;
        Close();
    }

    private void ToggleMarks_OnClick(object? sender, RoutedEventArgs e)
    {
        _showExistingMarks = !_showExistingMarks;
        ToggleMarksItem.IsChecked = _showExistingMarks;
        RebuildList();
    }

    private void RefreshItems_OnClick(object? sender, RoutedEventArgs e)
    {
        RebuildAll();
    }

    private void ToggleCats_OnClick(object? sender, RoutedEventArgs e)
    {
        var show = !CatsScroll.IsVisible;
        CatsScroll.IsVisible = show;
        ToggleCatsItem.IsChecked = show;
        BodyGrid.ColumnDefinitions[0].Width = show ? new GridLength(200) : new GridLength(0);
    }

    private void ToggleCompact_OnClick(object? sender, RoutedEventArgs e)
    {
        _compact = !_compact;
        ToggleCompactItem.IsChecked = _compact;
        RebuildList();
    }

    private void ToggleHintId_OnClick(object? sender, RoutedEventArgs e)
    {
        _showHintId = !_showHintId;
        ToggleHintIdItem.IsChecked = _showHintId;
    }

    private void OpenDocs_OnClick(object? sender, RoutedEventArgs e)
    {
        UriNavigationCommands.UriNavigationCommand.Execute("https://docs.classisland.tech/app/automation.html");
    }

    // ---------- 键盘 ----------

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (!Root.IsVisible)
        {
            return;
        }
        var inSearch = e.Source == SearchTextBox || SearchTextBox.IsFocused;
        switch (e.Key)
        {
            case Key.Escape:
                if (_isSearchView)
                {
                    ClearSearch();
                }
                else
                {
                    Close();
                }
                e.Handled = true;
                break;
            case Key.Down:
                MoveCursor(1);
                e.Handled = true;
                break;
            case Key.Up:
                MoveCursor(-1);
                e.Handled = true;
                break;
            case Key.Enter:
                if (_cursor >= 0 && _cursor < _flat.Count)
                {
                    ActivateItem(_flat[_cursor]);
                }
                e.Handled = true;
                break;
            case Key.Oem2 or Key.Divide:
                if (!inSearch)
                {
                    SearchTextBox.Focus();
                    SearchTextBox.SelectAll();
                    e.Handled = true;
                }
                break;
        }
    }

    private void SearchTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        // 搜索跨全部分类进行，命中时整体切换为结果视图。
        _query = SearchTextBox.Text ?? "";
        _isSearchView = _query.Trim().Length > 0;
        _category = null;
        _cursor = 0;
        RebuildCategories();
        RebuildList();
    }

    private void ClearSearch()
    {
        _query = "";
        _isSearchView = false;
        _cursor = 0;
        SearchTextBox.Text = "";
        RebuildList();
        ShowDefaultHint();
    }

    // ---------- 主题感知画刷 ----------

    /// <summary>
    /// 以控件当前主题变体解析主题资源刷，避免暗色主题下解析到浅色变体的近黑前景。
    /// </summary>
    private IBrush GetBrush(string key) =>
        Application.Current?.TryGetResource(key, ActualThemeVariant, out var value) == true && value is IBrush brush
            ? brush
            : Brushes.Transparent;

    private void ApplyForeground(AvaloniaObject target, StyledProperty<IBrush?> property, string key) =>
        target.SetValue(property, GetBrush(key));
}

/// <summary>
/// 选择面板调用辅助。
/// </summary>
public static class AutomationPickerOverlayExtensions
{
    /// <summary>
    /// 从调用方打开选择面板。宿主页面未挂载面板时（如档案窗口、教程编辑器）为空操作，
    /// 调用方应自行保留旧选择入口。
    /// </summary>
    public static void ExecutePick(this Visual visual, AutomationPickerKind kind,
        Action<AutomationPickRequest>? configure = null)
    {
        var request = new AutomationPickRequest { Kind = kind };
        configure?.Invoke(request);
        AutomationPickerOverlay.Current?.Open(request);
    }
}

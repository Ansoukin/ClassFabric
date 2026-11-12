using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Models;
using ClassIsland.Services.Automation.Triggers;
using ClassIsland.Shared;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using ClassIsland.Models.SystemTools;
namespace ClassIsland.Services.SystemTools;

/// <summary>
/// 悬浮窗快捷遥控器服务。半透明圆角面板上按方案渲染触发器按钮行，
/// 支持置顶/置底两种层级、四种置底维持模式、整窗/按钮/行三级规则集显隐，
/// 以及触摸设备的专用拖拽把手。
/// </summary>
public class FloatingWindowService(
    ILogger<FloatingWindowService> logger,
    SettingsService settingsService,
    FloatingWindowProfileManager profileManager,
    ILessonsService lessonsService,
    IRulesetService rulesetService) : IHostedService
{
    private const uint EventSystemForeground = 0x0003;
    private const uint EventObjectReorder = 0x8004;
    private const uint WinEventOutOfContext = 0;
    private const uint WinEventSkipOwnProcess = 2;
    private const int WhMouseLl = 14;
    private const int WmMouseMove = 0x0200;
    private const int WmLButtonDown = 0x0201;
    private const int WmRButtonDown = 0x0204;
    private const ulong MiWpSignatureMask = 0xFFFFFF00UL;
    private const ulong MiWpSignature = 0xFF515700UL;
    private const int FollowClassIslandTheme = 0;
    private const int LightTheme = 1;
    private const int DarkTheme = 2;
    private const int AdaptiveBackgroundTheme = 3;
    private static readonly TimeSpan TouchLikeMouseGracePeriod = TimeSpan.FromMilliseconds(250);

    // SetWindowPos 的 hWndInsertAfter 特殊值：1 = HWND_BOTTOM，-1 = HWND_TOP。
    private static readonly IntPtr HwndBottom = new(1);
    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoSendChanging = 0x0400;

    private readonly Dictionary<FloatingWindowTrigger, FloatingWindowEntry> _entries = new();
    private Window? _window;
    private Grid? _windowRoot;
    private StackPanel? _stackPanel;
    private Border? _windowContainer;

    private readonly Dictionary<string, double> _buttonWidthCache = new();
    private double _lastButtonLayoutScale = double.NaN;
    private bool _allowWindowClose;
    private bool _restoringFromMinimized;
    private bool _isStarted;
    private bool _isStopped = true;
    private bool _windowBoundsClampQueued;
    private bool _pointerPressed;
    private bool _dragInitiated;
    private Point _pointerDownPoint;
    private PointerPressedEventArgs? _lastPressedArgs;

    private bool _isTouchDeviceDetected;
    private bool _touchDragAllowed;
    private PixelPoint _touchDragStartScreenPoint;
    private PixelPoint _touchDragStartWindowPosition;
    private Border? _touchDragHandle;
    private DateTime _lastTouchGeneratedMouseEventAt = DateTime.MinValue;

    private IntPtr _foregroundHook;
    private IntPtr _reorderHook;
    private WinEventProc? _winEventProc;
    private IntPtr _mouseHook;
    private LowLevelMouseProc? _lowLevelMouseProc;
    private DispatcherTimer LayerRecheck50MsTimer { get; } = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private DispatcherTimer LayerRecheck1MsTimer { get; } = new() { Interval = TimeSpan.FromMilliseconds(1) };

    private bool _rulesetHidingWindow;
    private readonly HashSet<string> _rulesetHiddenButtons = new();
    private readonly HashSet<int> _rulesetHiddenRows = new();

    private NativeMenuItem? _trayMenuItem;
    private Settings? _subscribedSettings;

    public event EventHandler? EntriesChanged;

    public IReadOnlyList<FloatingWindowEntry> Entries => _entries.Values.ToList();

    public FloatingWindowProfileManager ProfileManager => profileManager;

    #region 生命周期

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // 休眠态也要订阅设置变化：总开关从设置页打开时，靠这个回调把整条链路拉起来。
        settingsService.PropertyChanged += OnSettingsServicePropertyChanged;
        _subscribedSettings = settingsService.Settings;
        _subscribedSettings.PropertyChanged += OnSettingsPropertyChanged;
        AppBase.Current.AppStopping += OnAppStopping;

        if (OperatingSystem.IsWindows() && _subscribedSettings.IsFloatingWindowEnabled)
        {
            Start();
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        AppBase.Current.AppStopping -= OnAppStopping;
        if (_subscribedSettings != null)
        {
            _subscribedSettings.PropertyChanged -= OnSettingsPropertyChanged;
            _subscribedSettings = null;
        }

        settingsService.PropertyChanged -= OnSettingsServicePropertyChanged;
        Stop();
        return Task.CompletedTask;
    }

    private void OnAppStopping(object? sender, EventArgs e)
    {
        Stop();
    }

    /// <summary>
    /// 启动悬浮窗。总开关关闭或非 Windows 平台上保持休眠，不建窗口也不装钩子。
    /// </summary>
    public void Start()
    {
        if (_isStarted)
        {
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            logger.LogInformation("悬浮窗功能依赖 Win32 桌面接口，当前平台不启动。");
            return;
        }

        _isStarted = true;
        _isStopped = false;
        Dispatcher.UIThread.Post(() =>
        {
            if (_isStopped)
            {
                return;
            }

            profileManager.LoadProfile(settingsService.Settings.CurrentFloatingWindowProfile);
            EnsureWindow();
            EnsureLayerRecheckHooks();
            EnsureGlobalInputHooks();
            SubscribeThemeChanged();
            ApplyVisibility();
            RefreshLayerRecheckMode();
            RecheckWindowLayer();
            RefreshWindowButtons();
            RegisterOrUpdateTrayMenu();
        });
    }

    public void Stop()
    {
        if (!_isStarted)
        {
            return;
        }

        _isStarted = false;
        _isStopped = true;
        Dispatcher.UIThread.Post(() =>
        {
            UnregisterTrayMenu();
            if (_window != null)
            {
                // 退出前先把手头位置落盘，再销毁窗口。
                SavePosition(_window.Position);
                _allowWindowClose = true;
                _window.Close();
            }

            DiscardWindowState();

            LayerRecheck50MsTimer.Stop();
            LayerRecheck1MsTimer.Stop();
            RemoveLayerRecheckHooks();
            RemoveGlobalInputHooks();
            UnsubscribeThemeChanged();
        });
    }

    private void OnSettingsServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SettingsService.Settings))
        {
            return;
        }

        // 集控下发会整体替换 Settings 单例，订阅必须搬到新对象上，否则开关从此失聪。
        if (_subscribedSettings != null)
        {
            _subscribedSettings.PropertyChanged -= OnSettingsPropertyChanged;
        }

        _subscribedSettings = settingsService.Settings;
        _subscribedSettings.PropertyChanged += OnSettingsPropertyChanged;
        OnIsFloatingWindowEnabledChanged();
    }

    private void OnIsFloatingWindowEnabledChanged()
    {
        Dispatcher.UIThread.Post(() =>
        {
            RegisterOrUpdateTrayMenu();
            if (OperatingSystem.IsWindows() && settingsService.Settings.IsFloatingWindowEnabled)
            {
                Start();
            }
            else
            {
                Stop();
            }
        });
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Settings.IsFloatingWindowEnabled))
        {
            OnIsFloatingWindowEnabledChanged();
            return;
        }

        if (e.PropertyName == nameof(Settings.ShowFloatingWindow))
        {
            UpdateWindowState();
            // NativeMenuItem 不是线程安全对象，托盘头部的更新必须回到 UI 线程。
            Dispatcher.UIThread.Post(UpdateTrayMenuHeader);
            return;
        }

        if (e.PropertyName is nameof(Settings.FloatingWindowOpacity)
            or nameof(Settings.FloatingWindowTheme)
            or nameof(Settings.FloatingWindowScale)
            or nameof(Settings.FloatingWindowIconSize)
            or nameof(Settings.FloatingWindowTextSize)
            or nameof(Settings.FloatingWindowShadowEnabled)
            or nameof(Settings.FloatingWindowDragHandleAlwaysVisible))
        {
            Dispatcher.UIThread.Post(() =>
            {
                RefreshWindowButtons();
                ApplyWindowAppearance();
            });
        }
    }

    #endregion

    #region 托盘菜单

    private void RegisterOrUpdateTrayMenu()
    {
        var trayService = IAppHost.TryGetService<ITaskBarIconService>();
        if (trayService == null)
        {
            return;
        }

        if (_trayMenuItem == null)
        {
            _trayMenuItem = new NativeMenuItem();
            _trayMenuItem.Click += (_, _) =>
            {
                var settings = settingsService.Settings;
                if (!settings.IsFloatingWindowEnabled)
                {
                    return;
                }

                settings.ShowFloatingWindow = !settings.ShowFloatingWindow;
                UpdateWindowState();
                UpdateTrayMenuHeader();
            };
        }

        if (!settingsService.Settings.IsFloatingWindowEnabled)
        {
            UnregisterTrayMenu();
            return;
        }

        if (!trayService.MoreOptionsMenuItems.Contains(_trayMenuItem))
        {
            trayService.MoreOptionsMenuItems.Add(_trayMenuItem);
        }

        UpdateTrayMenuHeader();
    }

    private void UnregisterTrayMenu()
    {
        var trayService = IAppHost.TryGetService<ITaskBarIconService>();
        if (trayService == null || _trayMenuItem == null)
        {
            return;
        }

        if (trayService.MoreOptionsMenuItems.Contains(_trayMenuItem))
        {
            trayService.MoreOptionsMenuItems.Remove(_trayMenuItem);
        }
    }

    private void UpdateTrayMenuHeader()
    {
        if (_trayMenuItem == null)
        {
            return;
        }

        _trayMenuItem.Header = settingsService.Settings.ShowFloatingWindow
            ? "隐藏悬浮窗"
            : "显示悬浮窗";
    }

    #endregion

    #region 触发器注册表

    public void RegisterTrigger(FloatingWindowTrigger trigger)
    {
        _entries[trigger] = CreateEntry(trigger);

        PruneButtonWidthCache();
        NotifyEntriesChanged();
    }

    public void EnsureUniqueButtonIds()
    {
        var usedButtonIds = new HashSet<string>();
        var changed = false;

        foreach (var trigger in _entries.Keys.ToList())
        {
            var oldButtonId = trigger.GetButtonId();
            var buttonId = trigger.GetUniqueButtonId(usedButtonIds.Contains);
            usedButtonIds.Add(buttonId);
            _entries[trigger] = CreateEntry(trigger);

            if (!string.Equals(oldButtonId, buttonId, StringComparison.Ordinal))
            {
                changed = true;
            }
        }

        if (changed)
        {
            PruneButtonWidthCache();
        }
    }

    private FloatingWindowEntry CreateEntry(FloatingWindowTrigger trigger)
    {
        var buttonId = trigger.GetUniqueButtonId(id => _entries.Any(x =>
            !ReferenceEquals(x.Key, trigger) && string.Equals(x.Value.ButtonId, id, StringComparison.Ordinal)));

        return new FloatingWindowEntry(
            buttonId,
            trigger.GetIcon(),
            trigger.GetButtonName(),
            trigger.ShouldUseRevertStyle(),
            trigger.IsRevertEnabled(),
            trigger.GetLayoutButtonName(),
            trigger.TriggerFromFloatingWindow,
            trigger.CancelIsOnState);
    }

    public void UnregisterTrigger(FloatingWindowTrigger trigger)
    {
        if (_entries.Remove(trigger))
        {
            PruneButtonWidthCache();
            NotifyEntriesChanged();
        }
    }

    public void UpdateWindowState()
    {
        if (_isStopped) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (_isStopped) return;
            ApplyVisibility();
            RefreshLayerRecheckMode();
            RecheckWindowLayer();
            RefreshWindowButtons();
        });
    }

    private void NotifyEntriesChanged()
    {
        EntriesChanged?.Invoke(this, EventArgs.Empty);
        if (_isStopped) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (_isStopped) return;
            ApplyVisibility();
            RecheckWindowLayer();
            RefreshWindowButtons();
        });
    }

    #endregion

    #region 主题

    private void SubscribeThemeChanged()
    {
        if (Application.Current == null)
        {
            return;
        }

        Application.Current.PropertyChanged -= OnApplicationPropertyChanged;
        Application.Current.PropertyChanged += OnApplicationPropertyChanged;
    }

    private void UnsubscribeThemeChanged()
    {
        if (Application.Current == null)
        {
            return;
        }

        Application.Current.PropertyChanged -= OnApplicationPropertyChanged;
    }

    private void OnApplicationPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (string.Equals(e.Property?.Name, "ActualThemeVariant", StringComparison.Ordinal)
            && settingsService.Settings.FloatingWindowTheme == FollowClassIslandTheme)
        {
            Dispatcher.UIThread.Post(() =>
            {
                RefreshWindowButtons();
                ApplyWindowAppearance();
            });
        }
    }

    private ThemeVariant ResolveWindowThemeVariant()
    {
        return settingsService.Settings.FloatingWindowTheme switch
        {
            LightTheme => ThemeVariant.Light,
            DarkTheme => ThemeVariant.Dark,
            // 自适应背景档需要抓屏采样桌面亮度，抓屏链路已剔除，这里回退为跟随应用主题。
            AdaptiveBackgroundTheme => Application.Current?.ActualThemeVariant ?? ThemeVariant.Dark,
            _ => _window?.ActualThemeVariant ?? Application.Current?.ActualThemeVariant ?? ThemeVariant.Dark
        };
    }

    private bool IsLightTheme()
    {
        return ResolveWindowThemeVariant() == ThemeVariant.Light;
    }

    /// <summary>
    /// 设置悬浮窗主题
    /// </summary>
    /// <param name="theme">0=跟随 ClassIsland, 1=浅色, 2=深色, 3=自适应背景</param>
    public void SetWindowTheme(int theme)
    {
        var normalized = theme is LightTheme or DarkTheme or AdaptiveBackgroundTheme
            ? theme
            : FollowClassIslandTheme;
        if (settingsService.Settings.FloatingWindowTheme == normalized)
        {
            return;
        }

        settingsService.Settings.FloatingWindowTheme = normalized;
        Dispatcher.UIThread.Post(RefreshWindowButtons);
    }

    /// <summary>
    /// 切换到下一个悬浮窗主题
    /// </summary>
    public void ToggleWindowTheme()
    {
        var next = (settingsService.Settings.FloatingWindowTheme + 1) % 4;
        SetWindowTheme(next);
    }

    #endregion

    #region 窗口

    private void EnsureWindow()
    {
        if (_window != null || _isStopped)
        {
            return;
        }

        _allowWindowClose = false;
        _stackPanel = new StackPanel { Margin = new Thickness(6), Spacing = 6 };
        _windowContainer = new Border
        {
            CornerRadius = new CornerRadius(8),
            Child = _stackPanel
        };
        ApplyWindowAppearance();
        _windowRoot = new Grid
        {
            Children =
            {
                _windowContainer
            }
        };
        _window = new Window
        {
            Width = 64,
            Height = 64,
            ShowActivated = false,
            Topmost = settingsService.Settings.FloatingWindowLayer == 1,
            WindowDecorations = WindowDecorations.None,
            Background = Brushes.Transparent,
            TransparencyLevelHint = [WindowTransparencyLevel.Transparent],
            CanResize = false,
            ShowInTaskbar = false,
            SizeToContent = SizeToContent.WidthAndHeight,
            Content = _windowRoot
        };

        _window.Loaded += OnWindowLoaded;
        _window.SizeChanged += OnWindowSizeChanged;
        _window.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, true);
        _window.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel, true);
        _window.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel, true);
        _window.Closing += (_, e) =>
        {
            if (!_allowWindowClose)
            {
                e.Cancel = true;
                // 不在 Closing 事件中调用 Show()，窗口可能处于关闭过程中
            }
        };
        _window.PropertyChanged += OnWindowPropertyChanged;

        _window.Show();
    }

    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (!ReferenceEquals(sender, _window))
        {
            return;
        }

        QueueWindowBoundsClamp();
    }

    private void QueueWindowBoundsClamp()
    {
        if (_window == null || _windowBoundsClampQueued)
        {
            return;
        }

        var targetWindow = _window;
        _windowBoundsClampQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _windowBoundsClampQueued = false;
            if (!ReferenceEquals(_window, targetWindow))
            {
                return;
            }

            var clamped = ClampToVisibleScreen(targetWindow.Position);
            if (clamped != targetWindow.Position)
            {
                targetWindow.Position = clamped;
                SavePosition(clamped);
            }
        }, DispatcherPriority.Background);
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_window == null || _restoringFromMinimized)
        {
            return;
        }

        if (e.Property == Window.WindowStateProperty && _window.WindowState == WindowState.Minimized)
        {
            RestoreWindowFromMinimized();
        }
    }

    private void RestoreWindowFromMinimized()
    {
        if (_window == null || _restoringFromMinimized || _isStopped)
        {
            return;
        }

        _restoringFromMinimized = true;

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (_window == null || _isStopped)
                {
                    return;
                }

                if (!_window.IsVisible)
                {
                    try { _window.Show(); }
                    catch (InvalidOperationException)
                    {
                        DiscardWindowState();
                    }
                }

                if (_window != null)
                {
                    _window.WindowState = WindowState.Normal;
                }
            }
            finally
            {
                _restoringFromMinimized = false;
            }
        }, DispatcherPriority.Background);
    }

    private void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        if (_window == null || !ReferenceEquals(sender, _window))
        {
            return;
        }

        EnsureWindowPositionVisibleOnStartup();
        RecheckWindowLayer();
        ApplyWindowAppearance();
    }

    private void ApplyWindowAppearance()
    {
        if (_windowContainer == null)
        {
            return;
        }

        var settings = settingsService.Settings;
        var scale = Math.Clamp(settings.FloatingWindowScale, 0.5, 2.0);
        var opacity = Math.Clamp(settings.FloatingWindowOpacity, 10, 100) / 100.0;

        _windowContainer.CornerRadius = new CornerRadius(8);
        _windowContainer.Background = CreateFallbackWindowBrush(IsLightTheme(), opacity);
        _windowContainer.BoxShadow = settings.FloatingWindowShadowEnabled
            ? CreateFallbackShadow(IsLightTheme(), scale)
            : default;
    }

    private static IBrush CreateFallbackWindowBrush(bool isLightTheme, double opacity)
    {
        var alpha = (byte)Math.Round(255 * opacity);
        return new SolidColorBrush(isLightTheme
            ? Color.FromArgb(alpha, 0xFF, 0xFF, 0xFF)
            : Color.FromArgb(alpha, 0x1F, 0x1F, 0x1F));
    }

    private static BoxShadows CreateFallbackShadow(bool isLightTheme, double scale)
    {
        return new BoxShadows(new BoxShadow
        {
            OffsetX = 0,
            OffsetY = 6 * scale,
            Blur = 18 * scale,
            Spread = 0,
            Color = isLightTheme ? Color.Parse("#28000000") : Color.Parse("#60000000")
        });
    }

    private void ApplyVisibility()
    {
        if (_isStopped) return;
        EnsureWindow();
        if (_window == null)
        {
            return;
        }

        var shouldShow = settingsService.Settings.ShowFloatingWindow && HasAnyVisibleButton() && !_rulesetHidingWindow;

        if (shouldShow)
        {
            if (!_window.IsVisible)
            {
                try
                {
                    _window.Show();
                }
                catch (InvalidOperationException)
                {
                    logger.LogWarning("悬浮窗窗口状态失效，已重建。");
                    DiscardWindowState();
                    if (_isStopped) return;
                    EnsureWindow();
                    if (_window != null)
                    {
                        try { _window.Show(); }
                        catch (InvalidOperationException)
                        {
                            // 重建后仍失败，等下一次可见性巡检再试。
                        }
                    }
                }
            }
        }
        else
        {
            if (_window != null && _window.IsVisible)
            {
                try
                {
                    _window.Hide();
                }
                catch (InvalidOperationException)
                {
                    DiscardWindowState();
                }
            }
        }
    }

    private void DiscardWindowState()
    {
        _window = null;
        _windowRoot = null;
        _stackPanel = null;
        _windowContainer = null;
        _touchDragHandle = null;
        _windowBoundsClampQueued = false;
        _pointerPressed = false;
        _dragInitiated = false;
        _lastPressedArgs = null;
        _touchDragAllowed = false;
    }

    #endregion

    #region 按钮渲染

    private void RefreshWindowButtons()
    {
        if (_stackPanel == null)
        {
            return;
        }

        var settings = settingsService.Settings;
        var scale = Math.Clamp(settings.FloatingWindowScale, 0.5, 2.0);
        var iconSize = Math.Clamp(settings.FloatingWindowIconSize, 15, 50) * scale;
        var textSize = Math.Clamp(settings.FloatingWindowTextSize, 8, 30) * scale;
        var isLightTheme = IsLightTheme();
        var contentForeground = isLightTheme ? Brushes.Black : Brushes.White;

        // 缩放变化后旧的按钮宽度全部失真，整体作废重建，防止逐帧抖动。
        if (double.IsNaN(_lastButtonLayoutScale) || Math.Abs(_lastButtonLayoutScale - scale) > 0.0001)
        {
            _buttonWidthCache.Clear();
            _lastButtonLayoutScale = scale;
        }

        _stackPanel.Orientation = Orientation.Vertical;
        _stackPanel.Spacing = 6 * scale;
        _stackPanel.Margin = new Thickness(6 * scale);
        _stackPanel.HorizontalAlignment = HorizontalAlignment.Center;

        _stackPanel.Children.Clear();
        _touchDragHandle = null;

        int rowIndex = 0;
        foreach (var rowEntries in GetOrderedRows())
        {
            if (_rulesetHiddenRows.Contains(rowIndex))
            {
                rowIndex++;
                continue;
            }

            var rowPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6 * scale,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            foreach (var entry in rowEntries)
            {
                var iconBlock = FloatingWindowIconProvider.CreateIconControl(
                    entry.Icon, iconSize, contentForeground);

                var nameBlock = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(entry.Name) ? "触发" : entry.Name,
                    FontSize = textSize,
                    FontWeight = FontWeight.Normal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    TextTrimming = TextTrimming.None,
                    MaxWidth = 100 * scale,
                    Margin = new Thickness(0, 2 * scale, 0, 0),
                    Foreground = contentForeground
                };

                var contentPanel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Spacing = 2 * scale,
                    Children =
                    {
                        iconBlock,
                        nameBlock
                    }
                };

                var button = new Button
                {
                    Content = contentPanel,
                    MinWidth = 54 * scale,
                    MinHeight = 52 * scale,
                    Padding = new Thickness(6 * scale, 4 * scale),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = contentForeground,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center
                };

                if (entry.IsRevertStyleActive)
                {
                    // 等待恢复的按钮铺一层主题高亮底色并锁宽：宽度不锁会随"恢复"文本反复跳动。
                    button.Background = TryGetButtonPointerOverBrush() ??
                                        new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));

                    if (_buttonWidthCache.TryGetValue(entry.ButtonId, out var cachedWidth) && cachedWidth > 0)
                    {
                        button.Width = cachedWidth;
                    }
                }
                else
                {
                    button.Width = double.NaN;
                }

                if (!entry.IsRevertStyleActive)
                {
                    EventHandler? layoutUpdatedHandler = null;
                    layoutUpdatedHandler = (_, _) =>
                    {
                        var width = button.Bounds.Width;
                        if (width > 0)
                        {
                            _buttonWidthCache[entry.ButtonId] = width;
                            button.LayoutUpdated -= layoutUpdatedHandler;
                        }
                    };
                    button.LayoutUpdated += layoutUpdatedHandler;
                }

                button.PointerPressed += (_, e) =>
                {
                    if (!entry.IsRevertStyleActive || !entry.IsRevertEnabled)
                    {
                        return;
                    }

                    if (e.GetCurrentPoint(button).Properties.IsRightButtonPressed)
                    {
                        entry.CancelIsOnAction();
                        e.Handled = true;
                    }
                };

                button.Click += (_, _) => entry.TriggerAction();
                rowPanel.Children.Add(button);
            }

            _stackPanel.Children.Add(rowPanel);

            rowIndex++;
        }

        // 仅在"至少有一个可见按钮"时才显示拖拽把手，避免孤零零一个把手
        if (_stackPanel.Children.Count > 0)
        {
            _touchDragHandle = CreateTouchDragHandle(scale, contentForeground);
            UpdateDragHandleVisibility();
            _stackPanel.Children.Insert(0, _touchDragHandle);
        }
    }

    /// <summary>
    /// 判断是否至少有 1 个按钮在"经过规则集过滤后"是可见的。
    /// 用于避免悬浮窗在没有任何可见按钮时（被规则集全部隐藏）仍然显示。
    /// </summary>
    private bool HasAnyVisibleButton()
    {
        if (_entries.Count == 0)
        {
            return false;
        }

        var profile = profileManager.CurrentProfile;
        var rowConfigs = profile.FloatingWindowRowRulesets;
        var hiddenRowSet = new HashSet<int>();

        if (rowConfigs != null)
        {
            for (int i = 0; i < rowConfigs.Count; i++)
            {
                var cfg = rowConfigs[i];
                var shouldHide = !cfg.IsVisible
                    || (cfg.HideOnRule && cfg.HidingRules != null
                        && rulesetService.IsRulesetSatisfied(cfg.HidingRules));
                if (shouldHide)
                {
                    hiddenRowSet.Add(i);
                }
            }
        }

        int rowIndex = 0;
        foreach (var row in GetConfiguredButtonRowsWithFallback(profile))
        {
            if (!hiddenRowSet.Contains(rowIndex))
            {
                foreach (var id in row)
                {
                    if (_rulesetHiddenButtons.Contains(id))
                    {
                        continue;
                    }
                    foreach (var entry in _entries.Values)
                    {
                        if (string.Equals(entry.ButtonId, id, StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }
                }
            }
            rowIndex++;
        }

        return false;
    }

    private List<List<FloatingWindowEntry>> GetOrderedRows()
    {
        var profile = profileManager.CurrentProfile;
        var validButtonIds = _entries.Values.Select(x => x.ButtonId).ToHashSet();

        // 清理不存在的按钮ID
        if (profile.PruneInvalidButtonIds(validButtonIds))
        {
            profileManager.SaveProfile();
        }

        var values = _entries.Values
            .Where(x => !_rulesetHiddenButtons.Contains(x.ButtonId))
            .GroupBy(x => x.ButtonId)
            .ToDictionary(g => g.Key, g => g.First());

        var rows = new List<List<FloatingWindowEntry>>();

        foreach (var row in GetConfiguredButtonRowsWithFallback(profile))
        {
            var items = new List<FloatingWindowEntry>();
            foreach (var id in row)
            {
                if (values.TryGetValue(id, out var entry))
                {
                    items.Add(entry);
                }
            }
            if (items.Count > 0)
            {
                rows.Add(items);
            }
        }

        return rows;
    }


    private List<List<string>> GetConfiguredButtonRowsWithFallback(FloatingWindowProfile profile)
    {
        var validButtonIds = _entries.Values.Select(x => x.ButtonId).Distinct().ToList();
        var validSet = validButtonIds.ToHashSet();
        var rows = (profile.FloatingWindowButtonRows ?? [])
            .Select(row => row.Where(validSet.Contains).Distinct().ToList())
            .Where(row => row.Count > 0)
            .ToList();

        // 方案里没编排到的按钮统一兜底：没有行就整排新起，有行就挂到第一行末尾，
        // 否则新加的触发器永远不会出现在悬浮窗上。
        var configuredIds = rows.SelectMany(row => row).ToHashSet();
        var missingIds = validButtonIds
            .Where(id => !configuredIds.Contains(id))
            .Where(id => !profile.FloatingWindowButtonRulesets.ContainsKey(id))
            .ToList();

        if (missingIds.Count == 0)
        {
            return rows;
        }

        if (rows.Count == 0)
        {
            rows.Add(missingIds);
        }
        else
        {
            rows[0].AddRange(missingIds);
        }

        return rows;
    }

    private void PruneButtonWidthCache()
    {
        if (_buttonWidthCache.Count == 0)
        {
            return;
        }

        var validIds = _entries.Values.Select(x => x.ButtonId).ToHashSet();
        var staleIds = _buttonWidthCache.Keys.Where(id => !validIds.Contains(id)).ToList();
        foreach (var id in staleIds)
        {
            _buttonWidthCache.Remove(id);
        }
    }

    private static IBrush? TryGetButtonPointerOverBrush()
    {
        if (Application.Current == null)
        {
            return null;
        }

        if (Application.Current.TryGetResource("SubtleFillColorSecondaryBrush", null, out var subtle) &&
            subtle is IBrush subtleBrush)
        {
            return subtleBrush;
        }

        if (Application.Current.TryGetResource("ControlFillColorSecondaryBrush", null, out var control) &&
            control is IBrush controlBrush)
        {
            return controlBrush;
        }

        return null;
    }

    #endregion

    #region 拖拽

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_window == null)
        {
            return;
        }

        UpdateInputMode(e.Pointer.Type);

        if (_isTouchDeviceDetected)
        {
            // 触摸拖拽只认把手：手指落在按钮上应当触发按钮而不是挪窗口。
            if (!IsTouchLikePointer(e) || !IsEventFromTouchDragHandle(e.Source))
            {
                _touchDragAllowed = false;
                return;
            }

            _touchDragAllowed = true;
            _touchDragStartScreenPoint = _window.PointToScreen(e.GetPosition(_window));
            _touchDragStartWindowPosition = _window.Position;
            e.Pointer.Capture(_window);
            e.Handled = true;
            return;
        }

        if (!e.GetCurrentPoint(_window).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _pointerPressed = true;
        _dragInitiated = false;
        _pointerDownPoint = e.GetPosition(_window);
        _lastPressedArgs = e;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_window == null)
        {
            return;
        }

        UpdateInputMode(e.Pointer.Type);

        if (_isTouchDeviceDetected)
        {
            if (!IsTouchLikePointer(e) || !_touchDragAllowed)
            {
                return;
            }

            var screenPoint = _window.PointToScreen(e.GetPosition(_window));
            var deltaX = screenPoint.X - _touchDragStartScreenPoint.X;
            var deltaY = screenPoint.Y - _touchDragStartScreenPoint.Y;
            var target = new PixelPoint(_touchDragStartWindowPosition.X + deltaX,
                _touchDragStartWindowPosition.Y + deltaY);
            _window.Position = ClampToVisibleScreen(target);
            e.Handled = true;
            return;
        }

        if (!_pointerPressed)
        {
            return;
        }

        if (!_dragInitiated)
        {
            var point = e.GetPosition(_window);
            var dx = point.X - _pointerDownPoint.X;
            var dy = point.Y - _pointerDownPoint.Y;

            if (Math.Abs(dx) + Math.Abs(dy) < 4)
            {
                return;
            }

            _dragInitiated = true;
            // 经典路径交给系统的移动模态循环；4px 死区保证单击不误触发拖拽。
            if (_lastPressedArgs != null)
            {
                _window.BeginMoveDrag(_lastPressedArgs);
            }

            return;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_window == null)
        {
            return;
        }

        UpdateInputMode(e.Pointer.Type);

        if (_isTouchDeviceDetected)
        {
            if (!IsTouchLikePointer(e))
            {
                return;
            }

            var wasTouchDragging = _touchDragAllowed;
            _touchDragAllowed = false;
            if (!wasTouchDragging)
            {
                return;
            }

            e.Pointer.Capture(null);
            var touchClamped = ClampToVisibleScreen(_window.Position);
            _window.Position = touchClamped;
            SavePosition(touchClamped);
            e.Handled = true;
            return;
        }

        _pointerPressed = false;
        _dragInitiated = false;
        _lastPressedArgs = null;
        e.Pointer.Capture(null);

        var clamped = ClampToVisibleScreen(_window.Position);
        _window.Position = clamped;
        SavePosition(clamped);
    }

    private Border CreateTouchDragHandle(double scale, IBrush foreground)
    {
        var handle = new Border
        {
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(999),
            HorizontalAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(13 * scale, 5 * scale),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 3 * scale,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    CreateDragHandleDot(scale, foreground),
                    CreateDragHandleDot(scale, foreground),
                    CreateDragHandleDot(scale, foreground)
                }
            }
        };

        return handle;
    }

    private static Border CreateDragHandleDot(double scale, IBrush foreground)
    {
        return new Border
        {
            Width = 3 * scale,
            Height = 3 * scale,
            CornerRadius = new CornerRadius(999),
            Background = foreground,
            Opacity = 0.62
        };
    }

    private bool IsEventFromTouchDragHandle(object? source)
    {
        if (_touchDragHandle == null || source is not Visual visual)
        {
            return false;
        }

        var current = visual;
        while (current != null)
        {
            if (ReferenceEquals(current, _touchDragHandle))
            {
                return true;
            }

            current = current.GetVisualParent();
        }

        return false;
    }


    private bool IsTouchLikePointer(PointerEventArgs e)
    {
        return e.Pointer.Type == PointerType.Touch
               || (e.Pointer.Type == PointerType.Mouse && IsRecentTouchGeneratedMouseEvent());
    }

    private bool IsRecentTouchGeneratedMouseEvent()
    {
        return DateTime.UtcNow - _lastTouchGeneratedMouseEventAt <= TouchLikeMouseGracePeriod;
    }

    private void UpdateInputMode(PointerType pointerType)
    {
        if (pointerType == PointerType.Touch)
        {
            SetTouchInputMode(true);
            return;
        }

        if (pointerType == PointerType.Mouse)
        {
            // 触屏机器上手指会先被系统合成为鼠标事件，低级钩子打完标记后这里要当触摸处理。
            if (IsRecentTouchGeneratedMouseEvent())
            {
                SetTouchInputMode(true);
                return;
            }

            SetTouchInputMode(false);
            return;
        }

        if (pointerType == PointerType.Pen)
        {
            SetTouchInputMode(false);
        }
    }

    private void SetTouchInputMode(bool isTouch)
    {
        if (_isTouchDeviceDetected == isTouch)
        {
            return;
        }

        _isTouchDeviceDetected = isTouch;
        _pointerPressed = false;
        _dragInitiated = false;
        _lastPressedArgs = null;
        _touchDragAllowed = false;
        Dispatcher.UIThread.Post(UpdateDragHandleVisibility);
    }

    private void UpdateDragHandleVisibility()
    {
        if (_touchDragHandle == null)
        {
            return;
        }

        _touchDragHandle.IsVisible = _isTouchDeviceDetected ||
                                     settingsService.Settings.FloatingWindowDragHandleAlwaysVisible;
    }

    #endregion

    #region 触摸合成鼠标检测

    private void EnsureGlobalInputHooks()
    {
        if (_mouseHook != IntPtr.Zero)
        {
            return;
        }

        _lowLevelMouseProc ??= OnLowLevelMouse;
        _mouseHook = SetWindowsHookEx(WhMouseLl, _lowLevelMouseProc, IntPtr.Zero, 0);
    }

    private void RemoveGlobalInputHooks()
    {
        if (_mouseHook == IntPtr.Zero)
        {
            return;
        }

        UnhookWindowsHookEx(_mouseHook);
        _mouseHook = IntPtr.Zero;
    }

    private IntPtr OnLowLevelMouse(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0 || lParam == IntPtr.Zero)
        {
            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        var message = unchecked((uint)wParam.ToInt64());
        if (message != WmMouseMove && message != WmLButtonDown && message != WmRButtonDown)
        {
            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
        var extra = unchecked((ulong)info.dwExtraInfo.ToInt64());
        var isTouchGenerated = (extra & MiWpSignatureMask) == MiWpSignature;

        if (isTouchGenerated)
        {
            _lastTouchGeneratedMouseEventAt = DateTime.UtcNow;
            SetTouchInputMode(true);
        }
        else if (message == WmLButtonDown || message == WmRButtonDown)
        {
            SetTouchInputMode(false);
        }

        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    #endregion

    #region 位置与屏幕

    private PixelRect GetWindowRect(PixelPoint position)
    {
        if (_window == null)
        {
            return new PixelRect(position.X, position.Y, 0, 0);
        }

        var size = GetWindowPixelSize();
        return new PixelRect(position.X, position.Y, size.Width, size.Height);
    }

    private bool IsWindowInsideAnyScreen(PixelRect rect)
    {
        if (_window?.Screens?.All is not { } screens || screens.Count == 0)
        {
            return true;
        }

        return screens.Any(screen => screen.WorkingArea.Intersects(rect));
    }

    private PixelPoint GetCenteredPositionOnPrimaryScreen()
    {
        if (_window?.Screens?.Primary is not { } primary || _window == null)
        {
            return _window?.Position ?? new PixelPoint(0, 0);
        }

        var area = primary.WorkingArea;
        var size = GetWindowPixelSize();
        var width = size.Width;
        var height = size.Height;

        var x = area.X + (area.Width - width) / 2;
        var y = area.Y + (area.Height - height) / 2;
        return new PixelPoint(x, y);
    }

    private PixelPoint ClampToVisibleScreen(PixelPoint position)
    {
        if (_window == null)
        {
            return position;
        }

        var screens = _window.Screens?.All;
        if (screens == null || screens.Count == 0)
        {
            return position;
        }

        var screen = screens.FirstOrDefault(s => s.WorkingArea.Contains(position))
                     ?? _window.Screens?.Primary
                     ?? screens[0];

        var area = screen.WorkingArea;
        var size = GetWindowPixelSize();
        var width = size.Width;
        var height = size.Height;

        var minX = area.X;
        var minY = area.Y;
        var maxX = area.X + Math.Max(0, area.Width - width);
        var maxY = area.Y + Math.Max(0, area.Height - height);

        return new PixelPoint(Math.Clamp(position.X, minX, maxX), Math.Clamp(position.Y, minY, maxY));
    }

    private PixelSize GetWindowPixelSize()
    {
        if (_window == null)
        {
            return new PixelSize(1, 1);
        }

        var scaling = Math.Max(0.1, _window.RenderScaling);
        var dipSize = _window.ClientSize.Width > 0 && _window.ClientSize.Height > 0
            ? _window.ClientSize
            : new Size(_window.Bounds.Width, _window.Bounds.Height);
        return new PixelSize(
            Math.Max(1, (int)Math.Ceiling(dipSize.Width * scaling)),
            Math.Max(1, (int)Math.Ceiling(dipSize.Height * scaling)));
    }

    private void EnsureWindowPositionVisibleOnStartup()
    {
        if (_window == null)
        {
            return;
        }

        var configured = new PixelPoint(settingsService.Settings.FloatingWindowPositionX,
            settingsService.Settings.FloatingWindowPositionY);
        var rect = GetWindowRect(configured);
        var target = IsWindowInsideAnyScreen(rect) ? ClampToVisibleScreen(configured) : GetCenteredPositionOnPrimaryScreen();

        _window.Position = target;
        SavePosition(target, forceSave: configured != target);
    }

    private void SavePosition(PixelPoint position, bool forceSave = false)
    {
        var changed = false;

        if (settingsService.Settings.FloatingWindowPositionX != position.X)
        {
            settingsService.Settings.FloatingWindowPositionX = position.X;
            changed = true;
        }

        if (settingsService.Settings.FloatingWindowPositionY != position.Y)
        {
            settingsService.Settings.FloatingWindowPositionY = position.Y;
            changed = true;
        }

        // 设置属性变更会自动写回 Settings.json，这里只在位置没变但需要强制落盘时补一刀。
        if (forceSave && !changed)
        {
            settingsService.SaveSettings("悬浮窗位置");
        }
    }

    #endregion

    #region 层级维持

    private void EnsureLayerRecheckHooks()
    {
        _winEventProc ??= OnWinEvent;

        LayerRecheck50MsTimer.Tick -= OnLayerRecheck50MsTimerTick;
        LayerRecheck50MsTimer.Tick += OnLayerRecheck50MsTimerTick;
        LayerRecheck1MsTimer.Tick -= OnLayerRecheck1MsTimerTick;
        LayerRecheck1MsTimer.Tick += OnLayerRecheck1MsTimerTick;

        // 规则集巡检由 ILessonsService.PostMainTimerTicked 驱动
        lessonsService.PostMainTimerTicked -= OnPostMainTimerTicked;
        lessonsService.PostMainTimerTicked += OnPostMainTimerTicked;
    }

    private void RemoveLayerRecheckHooks()
    {
        if (_foregroundHook != IntPtr.Zero)
        {
            UnhookWinEvent(_foregroundHook);
            _foregroundHook = default;
        }

        if (_reorderHook != IntPtr.Zero)
        {
            UnhookWinEvent(_reorderHook);
            _reorderHook = default;
        }

        LayerRecheck50MsTimer.Tick -= OnLayerRecheck50MsTimerTick;
        LayerRecheck1MsTimer.Tick -= OnLayerRecheck1MsTimerTick;

        lessonsService.PostMainTimerTicked -= OnPostMainTimerTicked;
    }

    private void RefreshLayerRecheckMode()
    {
        var mode = settingsService.Settings.FloatingWindowLayerRecheckMode;
        var useReorderHook = mode == 0;
        var useForegroundHook = mode == 1;

        if (useForegroundHook)
        {
            EnsureForegroundHook();
        }
        else
        {
            RemoveForegroundHook();
        }

        if (useReorderHook)
        {
            EnsureReorderHook();
        }
        else
        {
            RemoveReorderHook();
        }

        LayerRecheck50MsTimer.IsEnabled = mode == 2;
        LayerRecheck1MsTimer.IsEnabled = mode == 3;
    }

    private void EnsureForegroundHook()
    {
        if (_foregroundHook != IntPtr.Zero || _winEventProc == null)
        {
            return;
        }

        _foregroundHook = SetWinEventHook(
            EventSystemForeground,
            EventSystemForeground,
            IntPtr.Zero,
            _winEventProc,
            0,
            0,
            WinEventOutOfContext | WinEventSkipOwnProcess);
    }

    private void EnsureReorderHook()
    {
        if (_reorderHook != IntPtr.Zero || _winEventProc == null)
        {
            return;
        }

        _reorderHook = SetWinEventHook(
            EventObjectReorder,
            EventObjectReorder,
            IntPtr.Zero,
            _winEventProc,
            0,
            0,
            WinEventOutOfContext | WinEventSkipOwnProcess);
    }

    private void RemoveForegroundHook()
    {
        if (_foregroundHook == IntPtr.Zero)
        {
            return;
        }

        UnhookWinEvent(_foregroundHook);
        _foregroundHook = default;
    }

    private void RemoveReorderHook()
    {
        if (_reorderHook == IntPtr.Zero)
        {
            return;
        }

        UnhookWinEvent(_reorderHook);
        _reorderHook = default;
    }

    private void OnPostMainTimerTicked(object? sender, EventArgs e)
    {
        // 模式 2/3 改回 DispatcherTimer Tick 事件触发，本回调只负责规则集巡检
        CheckFloatingWindowRuleset();
        CheckButtonRulesets();
        CheckRowRulesets();
        // 兜底 ApplyVisibility：避免所有按钮都被隐藏但窗口仍显示
        ApplyVisibility();
    }

    private void OnLayerRecheck50MsTimerTick(object? sender, EventArgs e)
    {
        if (settingsService.Settings.FloatingWindowLayerRecheckMode == 2)
        {
            RecheckWindowLayer();
        }
    }

    private void OnLayerRecheck1MsTimerTick(object? sender, EventArgs e)
    {
        if (settingsService.Settings.FloatingWindowLayerRecheckMode == 3)
        {
            RecheckWindowLayer();
        }
    }

    private void OnWinEvent(IntPtr hWinEventHook, uint @event, IntPtr hwnd, int idObject, int idChild, uint idEventThread,
        uint dwmsEventTime)
    {
        if (_window == null)
        {
            return;
        }

        var mode = settingsService.Settings.FloatingWindowLayerRecheckMode;
        var shouldRecheck = (@event == EventObjectReorder && mode == 0) ||
                            (@event == EventSystemForeground && mode == 1);
        if (!shouldRecheck)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            RecheckWindowLayer();
        });
    }

    private void RecheckWindowLayer()
    {
        if (_window == null)
        {
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            // 非 Windows 平台没有 HWND_BOTTOM 的等价物，只保留 Topmost 语义。
            _window.Topmost = settingsService.Settings.FloatingWindowLayer == 1;
            return;
        }

        var handle = _window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var flags = SwpNoSize | SwpNoMove | SwpNoActivate | SwpNoSendChanging;

        if (settingsService.Settings.FloatingWindowLayer == 0)
        {
            _window.Topmost = false;
            _ = SetWindowPos(handle, HwndBottom, 0, 0, 0, 0, flags);
            return;
        }

        _window.Topmost = true;
        _ = SetWindowPos(handle, HwndTopmost, 0, 0, 0, 0, flags);
    }

    public void ToggleWindowLayer()
    {
        SetWindowLayer(settingsService.Settings.FloatingWindowLayer == 1 ? 0 : 1);
    }

    public void SetWindowLayer(int layer)
    {
        settingsService.Settings.FloatingWindowLayer = layer == 1 ? 1 : 0;
        Dispatcher.UIThread.Post(() =>
        {
            if (_window != null)
            {
                _window.Topmost = settingsService.Settings.FloatingWindowLayer == 1;
            }
            RecheckWindowLayer();
            RefreshLayerRecheckMode();
        });
    }

    #endregion

    #region 方案切换

    public void ToggleWindowProfile()
    {
        var names = profileManager.GetProfileNames();
        if (names.Count <= 1)
        {
            return;
        }

        var currentName = profileManager.CurrentProfileName;
        var currentIndex = -1;
        for (int i = 0; i < names.Count; i++)
        {
            if (string.Equals(names[i], currentName, StringComparison.OrdinalIgnoreCase))
            {
                currentIndex = i;
                break;
            }
        }
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        var newIndex = (currentIndex + 1) % names.Count;
        var newName = names[newIndex];
        SwitchToProfile(newName);
    }

    public void SwitchToProfile(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            return;
        }

        var names = profileManager.GetProfileNames();
        if (!names.Contains(profileName))
        {
            return;
        }

        // 只在当前方案文件还存在时才保存，避免刚被删除的方案被重新写回磁盘
        if (profileManager.ProfileFileExists(profileManager.CurrentProfileName))
        {
            profileManager.SaveProfile();
        }
        profileManager.LoadProfile(profileName);
        settingsService.Settings.CurrentFloatingWindowProfile = profileName;

        Dispatcher.UIThread.Post(() =>
        {
            RefreshWindowButtons();
            ApplyVisibility();
            RecheckWindowLayer();
            RefreshLayerRecheckMode();
        });
    }

    #endregion

    #region 规则集巡检

    private void CheckFloatingWindowRuleset()
    {
        if (!settingsService.Settings.FloatingWindowRulesetEnabled)
        {
            if (_rulesetHidingWindow)
            {
                _rulesetHidingWindow = false;
                ApplyVisibility();
            }
            return;
        }

        var isSatisfied = rulesetService.IsRulesetSatisfied(settingsService.Settings.FloatingWindowRuleset);
        var shouldHide = isSatisfied;

        if (shouldHide != _rulesetHidingWindow)
        {
            _rulesetHidingWindow = shouldHide;
            ApplyVisibility();
        }
    }

    private void CheckButtonRulesets()
    {
        var profile = profileManager.CurrentProfile;

        var changed = false;
        foreach (var entry in _entries.Values)
        {
            if (!profile.FloatingWindowButtonRulesets.TryGetValue(entry.ButtonId, out var config))
            {
                continue;
            }

            var shouldHide = false;
            if (!config.IsVisible)
            {
                shouldHide = true;
            }
            else if (config.HideOnRule)
            {
                shouldHide = rulesetService.IsRulesetSatisfied(config.HidingRules);
            }

            var wasHidden = _rulesetHiddenButtons.Contains(entry.ButtonId);
            if (shouldHide != wasHidden)
            {
                if (shouldHide)
                {
                    _rulesetHiddenButtons.Add(entry.ButtonId);
                }
                else
                {
                    _rulesetHiddenButtons.Remove(entry.ButtonId);
                }
                changed = true;
            }
        }

        if (changed)
        {
            Dispatcher.UIThread.Post(RefreshWindowButtons);
        }
    }

    private void CheckRowRulesets()
    {
        var profile = profileManager.CurrentProfile;
        var rowConfigs = profile.FloatingWindowRowRulesets;
        if (rowConfigs == null || rowConfigs.Count == 0)
        {
            if (_rulesetHiddenRows.Count > 0)
            {
                _rulesetHiddenRows.Clear();
                Dispatcher.UIThread.Post(RefreshWindowButtons);
            }
            return;
        }

        var changed = false;
        for (int i = 0; i < rowConfigs.Count; i++)
        {
            var config = rowConfigs[i];
            var shouldHide = false;
            if (!config.IsVisible)
            {
                shouldHide = true;
            }
            else if (config.HideOnRule)
            {
                shouldHide = rulesetService.IsRulesetSatisfied(config.HidingRules);
            }

            var wasHidden = _rulesetHiddenRows.Contains(i);
            if (shouldHide != wasHidden)
            {
                if (shouldHide)
                {
                    _rulesetHiddenRows.Add(i);
                }
                else
                {
                    _rulesetHiddenRows.Remove(i);
                }
                changed = true;
            }
        }

        if (changed)
        {
            Dispatcher.UIThread.Post(RefreshWindowButtons);
        }
    }

    #endregion

    #region Win32 互操作

    // 主工程跨平台编译，这里手写 DllImport 而不是引用 Windows.Win32：
    // DllImport 是惰性绑定的，非 Windows 平台只要不进入调用点就不会解析，编译期无感。

    private delegate void WinEventProc(IntPtr hWinEventHook, uint @event, IntPtr hwnd, int idObject, int idChild, uint idEventThread,
        uint dwmsEventTime);

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventProc lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    #endregion
}

public record FloatingWindowEntry(
    string ButtonId,
    string Icon,
    string Name,
    bool IsRevertStyleActive,
    bool IsRevertEnabled,
    string LayoutName,
    Action TriggerAction,
    Action CancelIsOnAction);

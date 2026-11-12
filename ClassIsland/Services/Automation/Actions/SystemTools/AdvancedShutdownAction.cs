using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Icons;
using ClassIsland.Controls.SystemTools;
using ClassIsland.Models.Actions.SystemTools;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services.Automation.Actions.SystemTools;

/// <summary>
/// 高级计时关机。与「计时关机」的区别在于：倒计时由本程序自己的 PowerShell 子进程驱动，
/// 并弹出可操作的对话框（已阅 / 延长 / 取消 / 立即关机），主对话框最小化后缩成右上角悬浮条。
/// 行动 ID 与设置键名沿用社区插件 SystemTools 原值，老配置可无缝迁移。
/// </summary>
[AutomationCategory("电源选项")]
[ActionInfo("SystemTools.AdvancedShutdown", "高级计时关机", FluentIcons.ClockSparkleRegular, false)]
public class AdvancedShutdownAction(ILogger<AdvancedShutdownAction> logger) : ActionBase<AdvancedShutdownSettings>
{
    // 本工程桌面入口程序集是 ClassFabric.Desktop（插件版检查的是 ClassIsland.Desktop）
    private const string HostProcessName = "ClassFabric.Desktop";

    private readonly ILogger<AdvancedShutdownAction> _logger = logger;

    private static readonly object StateLock = new();
    private static DateTimeOffset _shutdownAt = DateTimeOffset.MinValue;
    private static int _totalScheduledSeconds;
    private static Process? _countdownProcess;
    private static AdvancedShutdownDialog? _activeDialog;
    private static Window? _floatingWindow;
    private static DispatcherTimer? _watchdogTimer;
    private static bool _allowMainDialogClose;
    private static bool _allowFloatingWindowClose;
    private static int _appStoppingHandled;

    /// <summary>
    /// 行动是 transient 注册、每次触发都会 new 一个实例，所以退出钩子不能放实例构造函数里，否则会重复订阅。
    /// </summary>
    private static int _appStoppingHooked;

    /// <summary>
    /// 倒计时进程是本程序自己拉起的，应用退出时若不清理，关机计划会在无人值守时继续执行。
    /// 插件版在退出前用 WinForms NativeWindow 监听 WM_QUERYENDSESSION 区分「系统关机/注销」与「应用主动退出」；
    /// 主工程不开 WinForms，也未直接引用 Microsoft.Win32.SystemEvents，无法做这个区分。
    /// 这里保守处理：应用退出时一律按会话结束对待，只终止自己的倒计时进程，不补发 shutdown /a，
    /// 避免在系统级关机流程里误取消系统排定的关机任务。
    /// </summary>
    private static void EnsureAppStoppingHooked()
    {
        if (Interlocked.Exchange(ref _appStoppingHooked, 1) != 0)
        {
            return;
        }

        AppBase.Current.AppStopping += (_, _) => CancelPlanOnAppStopping(true);
    }

    public static bool CancelPlanOnAppStopping(bool isSessionEnding)
    {
        lock (StateLock)
        {
            if (_appStoppingHandled != 0)
            {
                return false;
            }

            _appStoppingHandled = 1;
            StopCountdownProcessUnsafe();
            if (!isSessionEnding)
            {
                TryAbortSystemShutdown();
            }
        }

        return true;
    }

    protected override async Task OnInvoke()
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogWarning("高级计时关机依赖 shutdown/powershell 命令，仅支持 Windows，跳过执行。");
            await base.OnInvoke();
            return;
        }

        if (!IsHostRunning())
        {
            _logger.LogWarning("未检测到 {HostProcess}，取消启动高级计时关机。", HostProcessName);
            StopAllStates();
            return;
        }

        var configuredMinutes = Math.Max(1, Settings?.Minutes ?? 2);
        EnsureAppStoppingHooked();
        if (!ScheduleShutdown(configuredMinutes))
        {
            return;
        }

        await ShowDialogAsync();
        await base.OnInvoke();
    }

    private static bool IsHostRunning()
    {
        try
        {
            return Process.GetProcessesByName(HostProcessName).Length > 0;
        }
        catch
        {
            return true;
        }
    }

    private static bool IsPlanActive()
    {
        lock (StateLock)
        {
            return _countdownProcess is { HasExited: false } && _shutdownAt > DateTimeOffset.Now;
        }
    }

    private bool ScheduleShutdown(int minutes)
    {
        var safeMinutes = Math.Max(1, minutes);
        var seconds = checked(safeMinutes * 60);

        lock (StateLock)
        {
            if (_appStoppingHandled != 0)
            {
                return false;
            }

            if (IsPlanActive())
            {
                return true;
            }

            // The process and the UI deadline form one plan. Cancellation
            // must not run between publishing its deadline and its process.
            StartOrReplaceCountdownProcessUnsafe(seconds);
        }

        EnsureWatchdogRunning();
        return true;
    }

    private async Task AssignProgressAnimatorAsync(ProgressBar bar, TimeSpan targetTime, TimeSpan totalTime)
    {
        try
        {
            var safeTotalMilliseconds = Math.Max(1, totalTime.TotalMilliseconds);
            var animation = new Animation
            {
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0),
                        Setters =
                        {
                            new Setter(RangeBase.ValueProperty,
                                3000.0 * targetTime.TotalMilliseconds / safeTotalMilliseconds)
                        }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1),
                        Setters =
                        {
                            new Setter(RangeBase.ValueProperty, 0.0),
                        }
                    }
                },
                Duration = targetTime,
                FillMode = FillMode.Forward
            };

            await animation.RunAsync(bar);
        }
        catch (Exception ex)
        {
            // The dialog can close while the animation is running. Observe
            // and log that cancellation/lifecycle failure instead of creating
            // an unobserved task exception.
            _logger.LogDebug(ex, "关机倒计时进度动画已停止");
        }
    }

    private void ExtendShutdown(int extendMinutes)
    {
        var safeExtendMinutes = Math.Max(1, extendMinutes);

        lock (StateLock)
        {
            if (_appStoppingHandled != 0 || !IsPlanActive())
            {
                return;
            }

            var totalSeconds = checked((int)Math.Ceiling((_shutdownAt - DateTimeOffset.Now).TotalSeconds)
                                       + checked(safeExtendMinutes * 60));
            StartOrReplaceCountdownProcessUnsafe(totalSeconds);
        }
    }

    private void CancelShutdownPlan()
    {
        StopAllStates();
    }

    private void StartOrReplaceCountdownProcessUnsafe(int seconds)
    {
        var safeSeconds = Math.Max(60, seconds);
        StopCountdownProcessUnsafe();

        try
        {
            // 用独立 PowerShell 子进程而非本程序计时器，是为了让关机计划不随本程序崩溃而丢失。
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"Start-Sleep -Seconds {safeSeconds}; shutdown.exe /s /t 0\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            _countdownProcess = Process.Start(psi)
                ?? throw new InvalidOperationException("无法启动计时关机进程。");
            _shutdownAt = DateTimeOffset.Now.AddSeconds(safeSeconds);
            _totalScheduledSeconds = safeSeconds;
            _logger.LogInformation("已启动 Windows 计时关机进程，{Seconds} 秒后执行关机。", safeSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动计时关机进程失败。秒数: {Seconds}", safeSeconds);
            throw;
        }
    }

    private static void StopCountdownProcessUnsafe()
    {
        try
        {
            if (_countdownProcess is { HasExited: false })
            {
                _countdownProcess.Kill(true);
            }
        }
        catch
        {
        }
        finally
        {
            _countdownProcess?.Dispose();
            _countdownProcess = null;
            _shutdownAt = DateTimeOffset.MinValue;
            _totalScheduledSeconds = 0;
        }
    }

    private static void TryAbortSystemShutdown()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "shutdown",
                Arguments = "/a",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            using var process = Process.Start(psi);
            process?.WaitForExit(1000);
        }
        catch
        {
        }
    }

    private static int GetRemainingSeconds()
    {
        lock (StateLock)
        {
            return (int)Math.Clamp(Math.Ceiling((_shutdownAt - DateTimeOffset.Now).TotalSeconds), 0, int.MaxValue);
        }
    }

    private static double BuildCountdownProgress()
    {
        int remaining;
        int total;
        lock (StateLock)
        {
            remaining = GetRemainingSeconds();
            total = _totalScheduledSeconds;
        }

        if (total <= 0)
        {
            return 0;
        }

        return Math.Clamp(remaining * 100.0 / total, 0, 100);
    }

    private static string BuildCountdownText()
    {
        var remainingSeconds = GetRemainingSeconds();
        var minutes = remainingSeconds / 60;
        var seconds = remainingSeconds % 60;
        return $"距离关机还有{minutes}分{seconds:00}秒";
    }

    private void EnsureWatchdogRunning()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_watchdogTimer != null || !IsPlanActive())
            {
                return;
            }

            _watchdogTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _watchdogTimer.Tick += (_, _) =>
            {
                if (!IsHostRunning())
                {
                    _logger.LogWarning("检测到 {HostProcess} 已退出，立即停止高级计时关机。", HostProcessName);
                    StopAllStates();
                    return;
                }

                if (!IsPlanActive())
                {
                    StopAllStates(onlyIfInactive: true);
                }
            };
            _watchdogTimer.Start();
        });
    }

    private void StopAllStates(bool onlyIfInactive = false)
    {
        lock (StateLock)
        {
            if (onlyIfInactive && IsPlanActive())
            {
                return;
            }

            StopCountdownProcessUnsafe();
            TryAbortSystemShutdown();
        }

        Dispatcher.UIThread.Post(() =>
        {
            lock (StateLock)
            {
                // A new plan may have started while this callback was queued.
                if (IsPlanActive())
                {
                    return;
                }

                _watchdogTimer?.Stop();
                _watchdogTimer = null;
                CloseMainDialogProgrammatically();
                CloseFloatingWindowProgrammatically();
            }
        });
    }

    private async Task ShowDialogAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (!IsPlanActive())
            {
                return;
            }

            CloseFloatingWindowProgrammatically();
            await ShowStyledDialogAsync();
        });
    }

    private async Task ShowStyledDialogAsync()
    {
        if (_activeDialog is { IsVisible: true })
        {
            _activeDialog.Activate();
            return;
        }

        var dialog = new AdvancedShutdownDialog
        {
            CanResize = false
        };
        _activeDialog = dialog;

        dialog.Closing += (_, e) =>
        {
            if (!_allowMainDialogClose && IsPlanActive())
            {
                e.Cancel = true;
            }
        };

        var textBlock = dialog.CountdownTextBlock ?? throw new InvalidOperationException("CountdownTextBlockElement 未找到");
        var progressBar = dialog.CountdownProgressBar ?? throw new InvalidOperationException("CountdownProgressBarElement 未找到");
        var immediateShutdownButton = dialog.ImmediateShutdownButton ?? throw new InvalidOperationException("ImmediateShutdownButtonElement 未找到");
        var readButton = dialog.ReadButton ?? throw new InvalidOperationException("ReadButtonElement 未找到");
        var cancelPlanButton = dialog.CancelPlanButton ?? throw new InvalidOperationException("CancelPlanButtonElement 未找到");
        var extendButton = dialog.ExtendButton ?? throw new InvalidOperationException("ExtendButtonElement 未找到");

        textBlock.Text = BuildCountdownText();
        progressBar.Value = BuildCountdownProgress();

        var countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        countdownTimer.Tick += (_, _) =>
        {
            textBlock.Text = BuildCountdownText();

            if (!IsPlanActive())
            {
                CloseMainDialogProgrammatically();
            }
        };
        lock (StateLock)
        {
            _ = AssignProgressAnimatorAsync(progressBar, TimeSpan.FromSeconds(GetRemainingSeconds()),
                TimeSpan.FromSeconds(_totalScheduledSeconds));
        }
        countdownTimer.Start();

        dialog.Closed += (_, _) =>
        {
            countdownTimer.Stop();
            if (ReferenceEquals(_activeDialog, dialog))
            {
                _activeDialog = null;
            }

            if (IsPlanActive())
            {
                ShowOrUpdateFloatingWindow();
            }
        };

        readButton.Click += (_, _) =>
        {
            CloseMainDialogProgrammatically();
            ShowOrUpdateFloatingWindow();
        };

        immediateShutdownButton.Click += (_, _) =>
        {
            StopAllStates();
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "shutdown",
                    Arguments = "/s /t 0",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行立即关机失败。");
            }
        };

        cancelPlanButton.Click += (_, _) => CancelShutdownPlan();

        extendButton.Click += async (_, _) =>
        {
            var extendMinutes = await ShowExtendInputDialogAsync(dialog);
            if (extendMinutes.HasValue)
            {
                ExtendShutdown(extendMinutes.Value);
                CloseMainDialogProgrammatically();
                ShowOrUpdateFloatingWindow();
            }
        };

        dialog.Show();
        dialog.Activate();
        await Task.CompletedTask;
    }

    private void ShowOrUpdateFloatingWindow()
    {
        if (!IsPlanActive())
        {
            CloseFloatingWindowProgrammatically();
            return;
        }

        if (_floatingWindow is { IsVisible: true })
        {
            return;
        }

        var tipButton = new Button
        {
            Content = BuildCountdownText() + "  点此返回设置",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = Brushes.Transparent,
            Foreground = Brushes.White
        };

        tipButton.Click += async (_, _) => await ShowDialogAsync();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) =>
        {
            if (!IsPlanActive())
            {
                CloseFloatingWindowProgrammatically();
                return;
            }

            tipButton.Content = BuildCountdownText() + "  点此返回设置";
        };

        var floatWindow = new Window
        {
            Width = 320,
            Height = 56,
            CanResize = false,
            Topmost = true,
            ShowInTaskbar = false,
            WindowDecorations = WindowDecorations.None,
            Background = Brushes.Transparent,
            TransparencyLevelHint = [WindowTransparencyLevel.Transparent],
            Content = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 6),
                Background = new SolidColorBrush(Color.FromArgb(210, 20, 20, 20)),
                Opacity = 0.88,
                Child = tipButton
            }
        };

        floatWindow.Opened += (_, _) => PinFloatingWindowTopRight(floatWindow);
        floatWindow.PositionChanged += (_, _) => PinFloatingWindowTopRight(floatWindow);
        floatWindow.Closing += (_, e) =>
        {
            if (!_allowFloatingWindowClose && IsPlanActive())
            {
                e.Cancel = true;
                PinFloatingWindowTopRight(floatWindow);
            }
        };
        floatWindow.Closed += (_, _) =>
        {
            timer.Stop();
            if (ReferenceEquals(_floatingWindow, floatWindow))
            {
                _floatingWindow = null;
            }
        };

        _floatingWindow = floatWindow;
        floatWindow.Show();
        timer.Start();
    }

    private static void PinFloatingWindowTopRight(Window window)
    {
        var screen = window.Screens.ScreenFromWindow(window) ?? window.Screens.Primary;
        if (screen is null)
        {
            return;
        }

        var area = screen.WorkingArea;
        var scaling = Math.Max(0.5, window.RenderScaling);
        var widthDip = window.Bounds.Width > 0 ? window.Bounds.Width : window.Width;
        var marginPx = (int)Math.Round(12 * scaling);
        var widthPx = (int)Math.Round(widthDip * scaling);

        var x = area.X + area.Width - widthPx - marginPx;
        var y = area.Y + marginPx;
        var target = new PixelPoint(Math.Max(area.X, x), Math.Max(area.Y, y));

        if (window.Position != target)
        {
            window.Position = target;
        }
    }

    private void CloseMainDialogProgrammatically()
    {
        if (_activeDialog is not { } dialog)
        {
            return;
        }

        _allowMainDialogClose = true;
        dialog.Close();
        _allowMainDialogClose = false;
        _activeDialog = null;
    }

    private void CloseFloatingWindowProgrammatically()
    {
        if (_floatingWindow is not { } window)
        {
            return;
        }

        _allowFloatingWindowClose = true;
        window.Close();
        _allowFloatingWindowClose = false;
        _floatingWindow = null;
    }

    private static async Task<int?> ShowExtendInputDialogAsync(Window owner)
    {
        var dialog = new ExtendShutdownDialog
        {
            Topmost = true,
            ShowInTaskbar = false
        };

        var previousTopmost = owner.Topmost;
        owner.Topmost = false;
        try
        {
            await dialog.ShowDialog(owner);
            return dialog.ResultMinutes;
        }
        finally
        {
            owner.Topmost = previousTopmost;
            owner.Activate();
        }
    }
}

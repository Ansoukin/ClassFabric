using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Platforms.Abstraction;
using CommunityToolkit.Mvvm.ComponentModel;

using GuerrillaNtp;

using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace ClassIsland.Services;

public class ExactTimeService : ObservableRecipient, IExactTimeService
{
    /// <summary>
    /// 预置时间源主机名，顺序即备用源的尝试顺序；主源始终排在序列最前，不受此顺序影响。
    /// </summary>
    public static readonly string[] PresetServerHosts =
    [
        "ntp.aliyun.com",
        "ntp.tencent.com",
        "ntp.ntsc.ac.cn",
        "cn.pool.ntp.org",
        "time.windows.com",
        "time.apple.com",
        "time.google.com",
    ];

    private string _syncStatusMessage = "时间尚未同步。";

    private DateTime PrevDateTime { get; set; } = DateTime.MinValue;

    private bool NeedWaiting { get; set; } = false;

    private SettingsService SettingsService { get; }

    private NtpClock NtpClock { get; set; } = NtpClock.LocalFallback;

    private DateTime LastTime { get; set; } = DateTime.Now;

    private bool WaitingForSystemTimeChanged { get; set; } = false;

    private DateTime LastSystemTime { get; set; } = DateTime.Now;

    private Stopwatch TimeGetStopwatch { get; } = Stopwatch.StartNew();

    private ILogger<ExactTimeService> Logger { get; }

    private DispatcherTimer UpdateTimer { get; } = new DispatcherTimer()
    {
        Interval = TimeSpan.FromMinutes(10)
    };

    private bool _isSyncingTime = false;

    public string SyncStatusMessage
    {
        get => _syncStatusMessage;
        set
        {
            if (value == _syncStatusMessage) return;
            _syncStatusMessage = value;
            OnPropertyChanged();
        }
    }

    public ExactTimeService(SettingsService settingsService, ILogger<ExactTimeService> logger)
    {
        Logger = logger;
        SettingsService = settingsService;
        SettingsService.Settings.PropertyChanged += SettingsOnPropertyChanged;
        UpdateTimer.Tick += (sender, args) =>
        {
            _ = Task.Run(Sync);
        };
        Task.Run(() => {
            Sync();
            UpdateTimerStatus();
            PlatformServices.SystemEventsService.TimeChanged += SystemEventsOnTimeChanged;
            AppBase.Current.AppStopping += (sender, args) => PlatformServices.SystemEventsService.TimeChanged -= SystemEventsOnTimeChanged; ;
            
        });

        if (SettingsService.Settings.IsTimeAutoAdjustEnabled)
        {
            SettingsService.Settings.TimeOffsetSeconds += SettingsService.Settings.TimeAutoAdjustSeconds *
                                                          Math.Floor((DateTime.Today - SettingsService.Settings
                                                              .LastTimeAdjustDateTime.Date).TotalDays);
            SettingsService.Settings.TimeOffsetSeconds = Math.Round(SettingsService.Settings.TimeOffsetSeconds, 3);
        }
        SettingsService.Settings.LastTimeAdjustDateTime = DateTime.Today;
    }

    private void SystemEventsOnTimeChanged(object? sender, EventArgs e)
    {
        WaitingForSystemTimeChanged = true;
        Logger.LogInformation("系统时间修改，正在重新同步时间");
        Task.Run(Sync);
    }

    private void SettingsOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SettingsService.Settings.ExactTimeServer):
                // 服务器列表改动后下次同步直接按新列表建连接，这里无需缓存任何客户端。
                break;
            case nameof(SettingsService.Settings.IsExactTimeEnabled):
                UpdateTimerStatus();
                break;
        }
    }

    private void UpdateTimerStatus()
    {
        if (SettingsService.Settings.IsExactTimeEnabled)
        {
            UpdateTimer.Start();
        }
        else
        {
            UpdateTimer.Stop();
        }
    }

    /// <summary>
    /// 组装同步时实际尝试的时间源序列：用户选定的源最先，存储里遗留的自定义条目其次，
    /// 其余预置源殿后。这样用户只管选一个主源，某个源挂掉时自动换下一个，
    /// 全部源都无效才走错误链路，避免单一源故障就把日志刷屏。
    /// </summary>
    private List<string> GetConfiguredServers()
    {
        var servers = ParseServerList(SettingsService.Settings.ExactTimeServer);
        var chain = new List<string> { servers.FirstOrDefault() ?? "ntp.aliyun.com" };
        chain.AddRange(servers.Skip(1));
        chain.AddRange(PresetServerHosts.Where(x => !chain.Contains(x, StringComparer.OrdinalIgnoreCase)));
        return chain;
    }

    /// <summary>按分隔符切出服务器列表（去空白、去重、不回退默认值），供设置页共享同一解析口径。</summary>
    public static List<string> ParseServerList(string raw)
    {
        return (raw ?? string.Empty)
            .Split(['\r', '\n', ';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void Sync()
    {
        if (!SettingsService.Settings.IsExactTimeEnabled)
        {
            WaitingForSystemTimeChanged = false;
            return;
        }

        if (_isSyncingTime)
        {
            return;
        }
        _isSyncingTime = true;

        SyncStatusMessage = $"正在同步时间……";
        var prev = NtpClock.Now.LocalDateTime;
        try
        {
            List<string>? failures = null;
            NtpClock? synced = null;
            string? successServer = null;
            foreach (var server in GetConfiguredServers())
            {
                Logger.LogInformation("正在从 {} 同步时间", server);
                try
                {
                    synced = new NtpClient(server).Query();
                    successServer = server;
                    break;
                }
                catch (Exception ex)
                {
                    // 换源重试是预期内的正常分支，单个源失败不按错误级别上报，
                    // 只有下面全部源都无效时才走错误链路，避免日志刷屏。
                    (failures ??= new List<string>()).Add($"{server}: {ex.Message}");
                    Logger.LogInformation(ex, "时间源 {} 不可用，正在尝试下一个时间源。", server);
                }
            }

            if (synced == null)
            {
                var failureText = string.Join("；", failures ?? []);
                Logger.LogError("所有时间源同步失败：{}", failureText);
                SyncStatusMessage = $"同步时间失败：{failureText}";
                return;
            }

            NtpClock = synced;
            var nowBase = NtpClock.Now.LocalDateTime;
            if (Math.Abs((nowBase - prev).TotalSeconds) < 30 && nowBase < prev)
            {
                NeedWaiting = true;
                PrevDateTime = prev;
            }

            Logger.LogInformation("成功地从 {} 同步了时间，现在是 {}", successServer, nowBase.ToString());
            SyncStatusMessage = $"成功地在{nowBase}同步了时间（{successServer}）";
        }
        finally
        {
            LastSystemTime = DateTime.Now;
            TimeGetStopwatch.Restart();
            WaitingForSystemTimeChanged = false;
            _isSyncingTime = false;
        }
    }

    public DateTime GetCurrentLocalDateTime()
    {
        var systemTime = DateTime.Now;
        if (SettingsService.Settings.IsExactTimeEnabled)
        {
            if (Math.Abs((LastSystemTime - systemTime).TotalMilliseconds - TimeGetStopwatch.ElapsedMilliseconds) > 30_000.0 && !WaitingForSystemTimeChanged)
            {
                WaitingForSystemTimeChanged = true;
                Logger.LogInformation("检测到系统时间突变，已暂停返回的时间并重新同步时间");
                Task.Run(Sync);
            }
            if (WaitingForSystemTimeChanged)
            {
                return LastTime;
            }
        }

        LastSystemTime = systemTime;
        TimeGetStopwatch.Restart();
        var now = SettingsService.Settings.IsExactTimeEnabled ? NtpClock.Now.LocalDateTime : systemTime;
        DateTime baseTime;
        if (now < PrevDateTime && NeedWaiting)
        {
            baseTime = PrevDateTime;
        }
        else
        {
            if (NeedWaiting)
                NeedWaiting = false;
            baseTime = now;
        }
        return LastTime = baseTime + TimeSpan.FromSeconds(SettingsService.Settings.TimeOffsetSeconds +
                                                       SettingsService.Settings.DebugTimeOffsetSeconds);
    }
}

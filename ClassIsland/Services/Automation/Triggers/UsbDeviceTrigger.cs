using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Threading.Tasks;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Icons;
using ClassIsland.Models.Automation.Triggers;
using ClassIsland.Services.SystemTools;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services.Automation.Triggers;

[AutomationCategory("事件与集成")]
[TriggerInfo("SystemTools.UsbDeviceTrigger", "USB设备插入时", FluentIcons.UsbStickRegular)]
public class UsbDeviceTrigger : TriggerBase<UsbDeviceTriggerSettings>
{
    ILogger<UsbDeviceTrigger> Logger { get; } = App.GetService<ILogger<UsbDeviceTrigger>>();

    private ManagementEventWatcher? _volumeInsertWatcher;
    private readonly object _triggerSyncRoot = new();
    private readonly Dictionary<string, DateTime> _recentTriggerTime = new(StringComparer.OrdinalIgnoreCase);

    public override void Loaded()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        StartVolumeWatcher();
    }

    public override void UnLoaded()
    {
        StopVolumeWatcher();
    }

    private void StartVolumeWatcher()
    {
        // EventType = 2 => 配置变更事件里的“设备到达（卷插入）”。
        const string query = "SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 2";
        _volumeInsertWatcher = new ManagementEventWatcher(new WqlEventQuery(query));
        _volumeInsertWatcher.EventArrived += OnVolumeInserted;
        try
        {
            _volumeInsertWatcher.Start();
        }
        catch (Exception ex)
        {
            // WMI 是 Windows 标配组件，起不来只记日志，不做其它监听兜底。
            _volumeInsertWatcher.EventArrived -= OnVolumeInserted;
            _volumeInsertWatcher.Dispose();
            _volumeInsertWatcher = null;
            Logger.LogError(ex, "卷插入事件监听启动失败，USB 触发器不工作");
        }
    }

    private void StopVolumeWatcher()
    {
        if (_volumeInsertWatcher == null)
        {
            return;
        }

        _volumeInsertWatcher.EventArrived -= OnVolumeInserted;
        try
        {
            _volumeInsertWatcher.Stop();
        }
        catch
        {
            // 忽略停止过程中的异常，保证触发器卸载流程稳定。
        }

        _volumeInsertWatcher.Dispose();
        _volumeInsertWatcher = null;
    }

    private async void OnVolumeInserted(object? sender, EventArrivedEventArgs e)
    {
        try
        {
            var driveName = e.NewEvent.Properties["DriveName"]?.Value?.ToString();
            if (string.IsNullOrWhiteSpace(driveName))
            {
                return;
            }

            if (!TryNormalizeDriveRoot(driveName, out var driveRoot))
            {
                return;
            }

            // 判定与就绪等待涉及 WMI/IO，放到线程池执行，避免占用 WMI 回调线程。
            var key = await Task.Run(() => ResolveVolumeKeyAsync(driveRoot)).ConfigureAwait(false);
            if (key is null)
            {
                return;
            }

            if (!TryMarkTriggered(key))
            {
                return;
            }

            Trigger();
        }
        catch
        {
            // 单次卷事件处理失败不应影响后续事件。
        }
    }

    /// <summary>
    /// 按当前设置判定卷是否满足触发条件，并返回用于去重的键；不满足条件时返回 <c>null</c>。
    /// </summary>
    /// <remarks>「仅U盘设备」在每次卷事件到达时读取，勾选切换无需重建监听。</remarks>
    private async Task<string?> ResolveVolumeKeyAsync(string driveRoot)
    {
        string key;
        if (Settings.OnlyUsbStorage)
        {
            // Windows 常把 U 盘识别为固定磁盘，需按物理磁盘总线类型判定，不能只看 DriveType。
            var info = UsbStorageUtils.Classify(driveRoot);

            // USB 总线上的卷，或传统可移动介质卷，都视为“U 盘”类设备。
            if (!info.IsUsbStorage && !UsbStorageUtils.IsRemovableVolume(driveRoot))
            {
                return null;
            }

            // 同一物理磁盘的多个分区（如多分区启动盘）只应触发一次。
            key = info.DiskNumber is uint diskNumber ? $"usb-disk:{diskNumber}" : $"volume:{driveRoot}";

            // 卷刚插入时可能尚未挂载完成，等待就绪后再交由自动化处理。
            if (!await UsbStorageUtils.WaitUntilReadyAsync(driveRoot).ConfigureAwait(false))
            {
                return null;
            }
        }
        else
        {
            key = $"volume:{driveRoot}";
        }

        return key;
    }

    private bool TryMarkTriggered(string key)
    {
        var now = DateTime.Now;

        lock (_triggerSyncRoot)
        {
            if (now - Settings.LastTriggered < TimeSpan.FromSeconds(1))
            {
                return false;
            }

            if (_recentTriggerTime.TryGetValue(key, out var last) &&
                now - last < TimeSpan.FromSeconds(3))
            {
                return false;
            }

            Settings.LastTriggered = now;
            _recentTriggerTime[key] = now;
            return true;
        }
    }

    private static bool TryNormalizeDriveRoot(string driveName, out string driveRoot)
    {
        driveRoot = string.Empty;

        try
        {
            driveRoot = Path.GetPathRoot(driveName) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(driveRoot))
            {
                return false;
            }

            if (!driveRoot.EndsWith(Path.DirectorySeparatorChar))
            {
                driveRoot += Path.DirectorySeparatorChar;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}

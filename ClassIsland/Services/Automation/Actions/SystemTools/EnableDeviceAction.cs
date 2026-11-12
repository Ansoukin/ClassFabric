using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Icons;
using ClassIsland.Core.Models.Notification;
using ClassIsland.Models.Actions.SystemTools;
using ClassIsland.Services.NotificationProviders;
using ClassIsland.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services.Automation.Actions.SystemTools;

[AutomationCategory("实用工具")]
[ActionInfo("SystemTools.EnableDevice", "启用硬件设备", FluentIcons.ArrowCircleUpRegular, false)]
public class EnableDeviceAction(ILogger<EnableDeviceAction> logger) : ActionBase<EnableDeviceSettings>
{
    private readonly ILogger<EnableDeviceAction> _logger = logger;

    static SystemToolsNotificationProvider? NotificationProvider =>
        IAppHost.Host?.Services.GetServices<IHostedService>()
            .OfType<SystemToolsNotificationProvider>().FirstOrDefault();

    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        if (string.IsNullOrWhiteSpace(Settings.DeviceId))
        {
            _logger.LogWarning("设备ID为空");
            return;
        }

        // 生成 bat + PowerShell 脚本走 Get-PnpDevice/Enable-PnpDevice，Windows 专属能力。
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogWarning("启用硬件设备依赖 bat + PowerShell，仅在 Windows 上可用。");
            return;
        }

        try
        {
            // 用设备实例路径末 6 位区分不同设备的临时脚本，避免多设备互相覆盖。
            string deviceId = Settings.DeviceId;
            string lastSixChars = deviceId.Length >= 6 ? deviceId.Substring(deviceId.Length - 6) : deviceId;
            string pluginDir = Path.GetDirectoryName(GetType().Assembly.Location) ?? "";
            string batFilePath = Path.Combine(pluginDir, $"Enable_{lastSixChars}.bat");
            string ps1FilePath = Path.Combine(pluginDir, $"Enable_{lastSixChars}.ps1");

            string batContent = $@"@echo off
:: 检查并请求管理员权限
net session >nul 2>&1
if %errorLevel% NEQ 0 (
    powershell -Command ""Start-Process -FilePath '%~0' -Verb RunAs""
    exit /b
)

:: 以管理员身份隐藏运行 PowerShell 脚本
powershell.exe -WindowStyle Hidden -ExecutionPolicy Bypass -File ""%~dp0Enable_{lastSixChars}.ps1""";

            string ps1Content = $@"$device = Get-PnpDevice | Where-Object {{$_.InstanceId -eq ""{deviceId}""}}

if ($device) {{
    Enable-PnpDevice -InstanceId $device.InstanceId -Confirm:$false
    Write-Host ""已启用设备: $($device.FriendlyName) [ID: $($device.InstanceId)]""
}} else {{
    Write-Host ""未找到设备ID""
}}";

            await File.WriteAllTextAsync(batFilePath, batContent);
            await File.WriteAllTextAsync(ps1FilePath, ps1Content);

            _logger.LogInformation("已生成文件: {BatFile} 和 {Ps1File}", batFilePath, ps1FilePath);

            var psi = new ProcessStartInfo
            {
                FileName = batFilePath,
                UseShellExecute = true,
                CreateNoWindow = true
            };

            Process.Start(psi);

            _logger.LogInformation("已启动批处理文件");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启用设备失败");
            throw;
        }

        if (Settings.NotifyOnExecute)
        {
            NotificationProvider?.ShowNotification(new NotificationRequest
            {
                MaskContent = NotificationContent.CreateTwoIconsMask("已自动启用硬件设备", FluentIcons.IotAlertRegular, "")
            });
        }

        _logger.LogDebug("EnableDeviceAction OnInvoke 完成");
    }
}

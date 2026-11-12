using System;
using System.Linq;
using System.Management;
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

/// <summary>
/// 调整屏幕亮度。走 WMI 的 WmiSetBrightness，只有 Windows 且显示器支持才有效；
/// 台式机外接显示器通常不支持，必要时还需管理员权限。WMI 调用整段包在平台守卫里。
/// </summary>
[AutomationCategory("显示设置")]
[ActionInfo("SystemTools.AdjustScreenBrightness", "调整屏幕亮度", FluentIcons.WeatherSunnyFilled, false)]
public class AdjustScreenBrightnessAction(ILogger<AdjustScreenBrightnessAction> logger)
    : ActionBase<AdjustScreenBrightnessSettings>
{
    private readonly ILogger<AdjustScreenBrightnessAction> _logger = logger;

    protected override async Task OnInvoke()
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogWarning("WMI 亮度调整仅支持 Windows，跳过执行。");
            await base.OnInvoke();
            return;
        }

        await Task.Run(() =>
        {
            try
            {
                if (Settings == null)
                {
                    _logger.LogWarning("设置为空，无法调整屏幕亮度");
                    return;
                }

                if (Settings.BrightnessPercent < 0 || Settings.BrightnessPercent > 100)
                {
                    _logger.LogWarning("亮度值 {Brightness} 超出范围 0-100", Settings.BrightnessPercent);
                    throw new ArgumentOutOfRangeException(nameof(Settings.BrightnessPercent),
                        "亮度值必须在 0-100 之间");
                }

                bool anySuccess = SetScreenBrightnessWmi(Settings.BrightnessPercent);

                if (anySuccess)
                {
                    _logger.LogInformation("屏幕亮度已设置为 {Brightness}%", Settings.BrightnessPercent);
                }
                else
                {
                    _logger.LogWarning("设置屏幕亮度失败，可能是硬件不支持、WMI 调用失败或缺少管理员权限");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "调整屏幕亮度时发生错误");
                throw;
            }
        });

        if (Settings.NotifyOnExecute)
            IAppHost.Host?.Services.GetServices<IHostedService>().OfType<SystemToolsNotificationProvider>()
                .FirstOrDefault()?.ShowNotification(new NotificationRequest
                {
                    MaskContent = NotificationContent.CreateTwoIconsMask("已执行调整屏幕亮度操作", FluentIcons.IotAlertRegular, "")
                });

        await base.OnInvoke();
    }

    /// <param name="brightness">亮度值 0-100</param>
    /// <returns>是否至少有一台显示器设置成功</returns>
    private bool SetScreenBrightnessWmi(int brightness)
    {
        bool anySuccess = false;

        try
        {
            var scope = new ManagementScope(@"\\.\root\wmi");
            scope.Connect();

            var query = new ObjectQuery("SELECT * FROM WmiMonitorBrightnessMethods");
            using var searcher = new ManagementObjectSearcher(scope, query);
            var objects = searcher.Get();

            if (objects.Count == 0)
            {
                _logger.LogWarning("未找到支持亮度调整的显示器设备");
                return false;
            }

            foreach (ManagementObject obj in objects)
            {
                using (obj)
                {
                    try
                    {
                        ManagementBaseObject inParams = obj.GetMethodParameters("WmiSetBrightness");
                        inParams["Timeout"] = (uint)0;
                        inParams["Brightness"] = (byte)brightness;

                        ManagementBaseObject outParams = obj.InvokeMethod("WmiSetBrightness", inParams, null);

                        if (outParams != null)
                        {
                            uint returnValue = Convert.ToUInt32(outParams["returnValue"]);
                            _logger.LogInformation("显示器 {InstanceName} 亮度设置返回状态: {Status}",
                                obj["InstanceName"] ?? "Unknown", returnValue);

                            if (returnValue == 0)
                            {
                                anySuccess = true;
                            }
                            else
                            {
                                _logger.LogWarning("显示器 {InstanceName} 亮度设置失败，WMI 返回错误码: {ErrorCode}",
                                    obj["InstanceName"] ?? "Unknown", returnValue);
                            }
                        }
                    }
                    catch (ManagementException mex)
                    {
                        _logger.LogWarning(mex, "调用 WmiSetBrightness 失败（ManagementException）: {Message}", mex.Message);
                        if (mex.Message.Contains("Not supported", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogWarning("当前显示器不支持通过 WMI 调整亮度");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "调用 WmiSetBrightness 失败: {Message}", ex.Message);
                    }
                }
            }

            return anySuccess;
        }
        catch (ManagementException mex)
        {
            _logger.LogError(mex, "WMI 操作异常（ManagementException）: {Message}", mex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WMI 操作异常: {Message}", ex.Message);
            return false;
        }
    }
}

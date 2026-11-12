using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Icons;
using ClassIsland.Core.Models.Notification;
using ClassIsland.Models.Actions.SystemTools;
using ClassIsland.Platforms.Abstraction;
using ClassIsland.Services.NotificationProviders;
using ClassIsland.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services.Automation.Actions.SystemTools;

[AutomationCategory("系统个性化")]
[ActionInfo("SystemTools.ChangeWallpaper", "切换壁纸", FluentIcons.ImageArrowForwardRegular, false)]
public class ChangeWallpaperAction(ILogger<ChangeWallpaperAction> logger) : ActionBase<WallpaperSettings>
{
    private readonly ILogger<ChangeWallpaperAction> _logger = logger;

    static SystemToolsNotificationProvider? NotificationProvider =>
        IAppHost.Host?.Services.GetServices<IHostedService>()
            .OfType<SystemToolsNotificationProvider>().FirstOrDefault();

    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        if (Settings == null)
        {
            _logger.LogWarning("壁纸设置为空");
            return;
        }

        if (Settings.Mode == ChangeWallpaperMode.Image && string.IsNullOrWhiteSpace(Settings.ImagePath))
        {
            _logger.LogWarning("图片路径为空");
            return;
        }

        if (Settings.Mode == ChangeWallpaperMode.Image && !File.Exists(Settings.ImagePath))
        {
            _logger.LogError("图片文件不存在: {Path}", Settings.ImagePath);
            throw new FileNotFoundException("指定的图片文件不存在", Settings.ImagePath);
        }

        try
        {
            // 注册表与 SystemParametersInfo 的平台细节下沉到平台服务；非 Windows 桌面端是空操作。
            if (Settings.Mode == ChangeWallpaperMode.SolidColor)
            {
                var color = ParseColor(Settings.SolidColor);
                _logger.LogInformation("正在切换为纯色壁纸: {Color}", Settings.SolidColor);
                await Task.Run(() =>
                    PlatformServices.SystemEnvironmentService.SetWallpaperSolidColor(color.R, color.G, color.B));
                _logger.LogInformation("切换纯色壁纸成功: {Color}", Settings.SolidColor);
            }
            else
            {
                var imagePath = Settings.ImagePath;
                var fit = Settings.FitStyle;
                _logger.LogInformation("正在切换壁纸到: {Path}, FitStyle: {Fit}", imagePath, fit);

                await Task.Run(() =>
                    PlatformServices.SystemEnvironmentService.SetWallpaperImage(imagePath, fit));
                _logger.LogInformation("切换壁纸成功: {Path}", imagePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换壁纸失败");
            // 保持向上抛出异常，让上层 UI/宿主决定如何反馈给用户
            throw;
        }

        if (Settings.NotifyOnExecute)
        {
            NotificationProvider?.ShowNotification(new NotificationRequest
            {
                MaskContent = NotificationContent.CreateTwoIconsMask("已自动切换桌面壁纸", FluentIcons.IotAlertRegular, "")
            });
        }

        _logger.LogDebug("ChangeWallpaperAction OnInvoke 完成");
    }

    // 纯色壁纸颜色解析，支持 #RRGGBB 与 R,G,B 两种写法。
    private static (byte R, byte G, byte B) ParseColor(string value)
    {
        var color = value.Trim();
        if (color.StartsWith("#"))
        {
            color = color[1..];
        }

        if (color.Length == 6 && int.TryParse(color, NumberStyles.HexNumber, null, out var rgb))
        {
            return ((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));
        }

        var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 3 &&
            byte.TryParse(parts[0], out var r) &&
            byte.TryParse(parts[1], out var g) &&
            byte.TryParse(parts[2], out var b))
        {
            return (r, g, b);
        }

        throw new FormatException("纯色壁纸颜色格式无效，请使用 #RRGGBB 或 R,G,B。");
    }
}

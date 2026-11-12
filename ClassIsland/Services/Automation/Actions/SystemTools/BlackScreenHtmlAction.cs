using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform;
using ClassIsland.Core;
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

/// <summary>
/// 黑屏html。用默认浏览器打开一张纯黑网页并按 F11 全屏，伪装成息屏。
/// 插件版从插件目录读 black.html；主程序没有插件目录，改为从 Avalonia 资源释放到全局数据目录。
/// </summary>
[AutomationCategory("显示设置")]
[ActionInfo("SystemTools.BlackScreenHtml", "黑屏html", FluentIcons.DesktopOffRegular, false)]
public class BlackScreenHtmlAction(ILogger<BlackScreenHtmlAction> logger) : ActionBase<ShortcutKeyNotificationSettings>
{
    private static readonly Uri BlackScreenAssetUri = new("avares://ClassIsland/Assets/SystemTools/black.html");

    private const ushort VK_F11 = 0x7A;

    private readonly ILogger<BlackScreenHtmlAction> _logger = logger;

    protected override async Task OnInvoke()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                _logger.LogWarning("黑屏 html 依赖 cmd 与浏览器全屏，仅支持 Windows，跳过执行。");
                await base.OnInvoke();
                return;
            }

            var htmlPath = EnsureBlackScreenHtmlReleased();

            _logger.LogInformation("正在打开 black.html: {HtmlPath}", htmlPath);

            // start 的第一个带引号参数是窗口标题，所以路径前补一个空的 ""
            var psi = new ProcessStartInfo
            {
                FileName = "cmd",
                Arguments = $"/c start \"\" \"{htmlPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = Path.GetDirectoryName(htmlPath)
            };

            Process.Start(psi);

            // 浏览器起页需要一点时间，太快发 F11 会落空
            await Task.Delay(1000);

            var input = PlatformServices.SystemInputService;
            input.KeyDown(VK_F11);
            await Task.Delay(20);
            input.KeyUp(VK_F11);

            _logger.LogInformation("F11 全屏键已发送");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行黑屏html失败");
            throw;
        }

        if (Settings.NotifyOnExecute)
            IAppHost.Host?.Services.GetServices<IHostedService>().OfType<SystemToolsNotificationProvider>()
                .FirstOrDefault()?.ShowNotification(new NotificationRequest
                {
                    MaskContent = NotificationContent.CreateTwoIconsMask("已执行黑屏操作", FluentIcons.IotAlertRegular, "")
                });

        await base.OnInvoke();
    }

    /// <summary>
    /// 首次执行时把内嵌的 black.html 释放到全局数据目录，之后存在即复用，避免每次写盘。
    /// </summary>
    private string EnsureBlackScreenHtmlReleased()
    {
        var dir = Path.Combine(CommonDirectories.AppDataFolderPath, "SystemTools");
        Directory.CreateDirectory(dir);
        var htmlPath = Path.Combine(dir, "black.html");
        if (File.Exists(htmlPath))
        {
            return htmlPath;
        }

        _logger.LogInformation("首次执行，释放 black.html 到 {HtmlPath}", htmlPath);
        using (var stream = AssetLoader.Open(BlackScreenAssetUri))
        using (var file = File.Create(htmlPath))
        {
            stream.CopyTo(file);
        }

        return htmlPath;
    }
}

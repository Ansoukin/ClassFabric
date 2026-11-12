using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
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
/// 「键入内容」行动：把文本写入系统剪贴板后向前台窗口发一次 Ctrl+V。
/// </summary>
[AutomationCategory("模拟操作")]
[ActionInfo("SystemTools.TypeContent", "键入内容", FluentIcons.ClipboardTextEditRegular, false)]
public class TypeContentAction(ILogger<TypeContentAction> logger) : ActionBase<TypeContentSettings>
{
    private const ushort VkControl = 0x11;
    private readonly ILogger<TypeContentAction> _logger = logger;
    private const ushort VkV = 0x56;
    private const int KeyEventDelay = 20;

    static SystemToolsNotificationProvider? NotificationProvider { get; } =
        IAppHost.Host.Services.GetServices<IHostedService>().OfType<SystemToolsNotificationProvider>().FirstOrDefault();

    protected override async Task OnInvoke()
    {
        _logger.LogDebug("OnInvoke 开始");

        if (string.IsNullOrWhiteSpace(Settings.Content))
        {
            _logger.LogWarning("内容为空或空白");
            return;
        }

        var cancellationToken = InterruptCancellationToken;
        var controlKeyDown = false;
        var vKeyDown = false;
        try
        {
            _logger.LogInformation("正在键入内容");

            await SetClipboardTextAsync(Settings.Content);
            await Task.Delay(100, cancellationToken);

            // Ctrl+V 的发送窗口极短，两颗键全程记账，异常时在 finally 兜底释放，避免 Ctrl 卡住让用户摸不着头脑。
            controlKeyDown = true;
            PlatformServices.SystemInputService.KeyDown(VkControl);
            await Task.Delay(KeyEventDelay, cancellationToken);
            vKeyDown = true;
            PlatformServices.SystemInputService.KeyDown(VkV);
            await Task.Delay(KeyEventDelay, cancellationToken);
            PlatformServices.SystemInputService.KeyUp(VkV);
            vKeyDown = false;
            await Task.Delay(KeyEventDelay, cancellationToken);
            PlatformServices.SystemInputService.KeyUp(VkControl);
            controlKeyDown = false;

            _logger.LogInformation("内容已键入成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "键入内容失败");
            throw;
        }
        finally
        {
            if (vKeyDown)
            {
                try
                {
                    PlatformServices.SystemInputService.KeyUp(VkV);
                }
                catch (Exception releaseException)
                {
                    _logger.LogWarning(releaseException, "释放 Ctrl+V 的 V 键失败");
                }
            }

            if (controlKeyDown)
            {
                try
                {
                    PlatformServices.SystemInputService.KeyUp(VkControl);
                }
                catch (Exception releaseException)
                {
                    _logger.LogWarning(releaseException, "释放 Ctrl+V 的 Ctrl 键失败");
                }
            }
        }
        if (Settings.NotifyOnExecute)
            NotificationProvider?.ShowNotification(new NotificationRequest
            {
                MaskContent = NotificationContent.CreateTwoIconsMask("已自动键入内容", FluentIcons.IotAlertRegular, "")
            });


        await base.OnInvoke();
        _logger.LogDebug("OnInvoke 完成");
    }

    /// <summary>
    /// 经 Avalonia 剪贴板写文本。只在桌面端主窗口就绪时可用；剪贴板写不进去就抛错终止，
    /// 宁可报失败也不发一个粘贴旧内容的 Ctrl+V。
    /// </summary>
    private static async Task SetClipboardTextAsync(string text)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } win })
        {
            await win.Clipboard!.SetTextAsync(text);
            return;
        }

        throw new InvalidOperationException("当前运行环境没有可用的桌面剪贴板，无法键入内容。");
    }
}

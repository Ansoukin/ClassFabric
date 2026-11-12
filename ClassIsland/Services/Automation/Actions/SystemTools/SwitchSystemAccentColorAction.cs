using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Icons;
using ClassIsland.Core.Models.Notification;
using ClassIsland.Models.Actions.SystemTools;
using ClassIsland.Platforms.Abstraction;
using ClassIsland.Platforms.Abstraction.Services;
using ClassIsland.Services.NotificationProviders;
using ClassIsland.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services.Automation.Actions.SystemTools;

[AutomationCategory("系统个性化")]
[ActionInfo("SystemTools.SwitchSystemAccentColor", "切换系统强调色", FluentIcons.ColorFillRegular, false)]
public class SwitchSystemAccentColorAction(ILogger<SwitchSystemAccentColorAction> logger)
    : ActionBase<AccentColorSettings>
{
    private readonly ILogger<SwitchSystemAccentColorAction> _logger = logger;

    // 按行动组记录应用前的强调色快照，行动组恢复时用来还原。
    private static readonly ConcurrentDictionary<Guid, AccentColorSnapshot> Snapshots = new();

    static SystemToolsNotificationProvider? NotificationProvider =>
        IAppHost.Host?.Services.GetServices<IHostedService>()
            .OfType<SystemToolsNotificationProvider>().FirstOrDefault();

    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        if (Settings == null)
        {
            return;
        }

        if (!TryParseHex(Settings.ColorHex, out var color))
        {
            _logger.LogWarning("系统强调色颜色格式无效：{ColorHex}", Settings.ColorHex);
            return;
        }

        try
        {
            // 注册表字节序、未公开 uxtheme 导出与 DWM 参数等 Win32 细节都在平台实现里，这里只传 RGB。
            if (IsRevertable)
            {
                Snapshots[ActionSet.Guid] = PlatformServices.SystemEnvironmentService.CaptureAccentColor();
            }

            PlatformServices.SystemEnvironmentService.ApplyAccentColor(new AccentColorRgb(color.R, color.G, color.B));
            _logger.LogInformation("系统强调色已切换为 {ColorHex}", Settings.ColorHex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切换系统强调色失败");
            throw;
        }

        if (Settings.NotifyOnExecute)
        {
            NotificationProvider?.ShowNotification(new NotificationRequest
            {
                MaskContent = NotificationContent.CreateTwoIconsMask("已切换系统强调色", FluentIcons.ColorFillRegular, "")
            });
        }

        _logger.LogDebug("SwitchSystemAccentColorAction OnInvoke 完成");
    }

    protected override async Task OnRevert()
    {
        await base.OnRevert();

        if (!Snapshots.TryRemove(ActionSet.Guid, out var snapshot))
        {
            _logger.LogInformation("未找到系统强调色快照，跳过恢复。ActionSet={ActionSetGuid}", ActionSet.Guid);
            return;
        }

        try
        {
            PlatformServices.SystemEnvironmentService.RestoreAccentColor(snapshot);
            _logger.LogInformation("已恢复系统强调色。");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "恢复系统强调色失败");
        }
    }

    // 颜色解析逻辑改写自插件 Helpers.WindowsAccentColor.TryParseHex，只做解析不碰 Win32：
    // 支持 #RRGGBB、RRGGBB 以及带 Alpha 的 #AARRGGBB（Avalonia 颜色选择器输出带 Alpha，取后六位）。
    private static bool TryParseHex(string? hex, out (byte R, byte G, byte B) color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        var text = hex.Trim().TrimStart('#');
        if (text.Length == 8)
        {
            text = text[2..];
        }

        if (text.Length != 6 ||
            !byte.TryParse(text.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) ||
            !byte.TryParse(text.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) ||
            !byte.TryParse(text.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return false;
        }

        color = (r, g, b);
        return true;
    }
}

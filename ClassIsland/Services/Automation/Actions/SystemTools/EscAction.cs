using System;
using System.Threading.Tasks;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Icons;
using ClassIsland.Platforms.Abstraction;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services.Automation.Actions.SystemTools;

/// <summary>
/// 「按下 Esc 键」行动。
/// </summary>
[AutomationCategory("模拟操作")]
[ActionInfo("SystemTools.EscKey", "按下 Esc 键", FluentIcons.KeyboardRegular, false)]
public class EscAction(ILogger<EscAction> logger) : ActionBase
{
    private readonly ILogger<EscAction> _logger = logger;

    private const ushort VkEscape = 0x1B;
    private const int KeyPressDelay = 20;

    protected override async Task OnInvoke()
    {
        try
        {
            // 平台服务在非 Windows 桌面端是空操作，这里直接早退。
            if (!OperatingSystem.IsWindows())
            {
                _logger.LogWarning("当前平台不支持模拟按键");
                return;
            }

            _logger.LogInformation("正在模拟按下 Esc 键");

            PlatformServices.SystemInputService.KeyDown(VkEscape);
            await Task.Delay(KeyPressDelay);
            PlatformServices.SystemInputService.KeyUp(VkEscape);

            _logger.LogInformation("Esc 键已成功发送");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送 Esc 键失败");
            throw;
        }

        await base.OnInvoke();
    }
}

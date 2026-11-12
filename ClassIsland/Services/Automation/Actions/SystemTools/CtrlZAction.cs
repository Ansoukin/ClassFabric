using System;
using System.Threading.Tasks;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Icons;
using ClassIsland.Platforms.Abstraction;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services.Automation.Actions.SystemTools;

/// <summary>
/// 「按下 Ctrl+Z」行动。
/// </summary>
[AutomationCategory("模拟操作")]
[ActionInfo("SystemTools.CtrlZ", "按下 Ctrl+Z", FluentIcons.KeyboardRegular, false)]
public class CtrlZAction(ILogger<CtrlZAction> logger) : ActionBase
{
    private readonly ILogger<CtrlZAction> _logger = logger;

    private const ushort VkControl = 0x11; // Ctrl 键
    private const ushort VkZ = 0x5A; // Z 键
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

            _logger.LogInformation("正在模拟按下 Ctrl+Z");

            PlatformServices.SystemInputService.KeyDown(VkControl);
            await Task.Delay(KeyPressDelay);

            PlatformServices.SystemInputService.KeyDown(VkZ);
            await Task.Delay(KeyPressDelay);

            PlatformServices.SystemInputService.KeyUp(VkZ);
            await Task.Delay(KeyPressDelay);

            PlatformServices.SystemInputService.KeyUp(VkControl);

            _logger.LogInformation("Ctrl+Z 已成功发送");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送 Ctrl+Z 失败");
            throw;
        }

        await base.OnInvoke();
    }
}

using System;
using System.Threading.Tasks;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Icons;
using ClassIsland.Platforms.Abstraction;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services.Automation.Actions.SystemTools;

/// <summary>
/// 「按下 Alt+F4」行动。
/// </summary>
[AutomationCategory("模拟操作")]
[ActionInfo("SystemTools.AltF4", "按下 Alt+F4", FluentIcons.KeyboardRegular, false)]
public class AltF4Action(ILogger<AltF4Action> logger) : ActionBase
{
    private readonly ILogger<AltF4Action> _logger = logger;

    private const ushort VkMenu = 0x12; // Alt 键
    private const ushort VkF4 = 0x73; // F4 键
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

            _logger.LogInformation("正在模拟按下 Alt+F4");

            PlatformServices.SystemInputService.KeyDown(VkMenu);
            await Task.Delay(KeyPressDelay);

            PlatformServices.SystemInputService.KeyDown(VkF4);
            await Task.Delay(KeyPressDelay);

            PlatformServices.SystemInputService.KeyUp(VkF4);
            await Task.Delay(KeyPressDelay);

            PlatformServices.SystemInputService.KeyUp(VkMenu);

            _logger.LogInformation("Alt+F4 已成功发送");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送 Alt+F4 失败");
            throw;
        }

        await base.OnInvoke();
    }
}

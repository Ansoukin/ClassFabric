using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ClassFabric.Core.Abstractions.Automation;
using ClassFabric.Core.Attributes;
using ClassFabric.Models.Actions;
using ClassFabric.Services.Automation.PlatformTools;
using ClassFabric.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace ClassFabric.Services.Automation.Actions;

[ActionInfo("classfabric.action.typeContent", "键入内容", "\uED7E")]
public class TypeContentAction : ActionBase<TypeContentActionSettings>
{
    private InputSimulationService InputSimulationService =>
        IAppHost.Host.Services.GetRequiredService<InputSimulationService>();

    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        var content = Settings.Content ?? string.Empty;
        if (Settings.IsFromClipboard)
        {
            var clipboardText = await GetClipboardTextAsync();
            if (!string.IsNullOrEmpty(clipboardText))
                content = clipboardText;
        }

        if (content.Length == 0) return;
        InputSimulationService.TypeText(content);
    }

    static async Task<string?> GetClipboardTextAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime lifetime ||
            lifetime.MainWindow is not { } window)
            return null;
        try
        {
            return await TopLevel.GetTopLevel(window)?.Clipboard?.GetTextAsync();
        }
        catch (Exception)
        {
            // 剪贴板可能被其他进程占用而读取失败，此时回退到设置里手动填写的内容。
            return null;
        }
    }
}

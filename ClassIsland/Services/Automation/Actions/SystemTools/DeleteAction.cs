using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Icons;
using ClassIsland.Models.Actions.SystemTools;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services.Automation.Actions.SystemTools;

[AutomationCategory("文件操作")]
[ActionInfo("SystemTools.Delete", "删除", FluentIcons.DeleteRegular, false)]
public class DeleteAction(ILogger<DeleteAction> logger) : ActionBase<DeleteSettings>
{
    private readonly ILogger<DeleteAction> _logger = logger;

    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        if (Settings == null || string.IsNullOrWhiteSpace(Settings.TargetPath))
        {
            _logger.LogWarning("路径为空");
            return;
        }

        try
        {
            // 保留卷根（如 C:\）原样。裁成 C: 会变成驱动器相对路径，可能落到错误目录。
            var targetPath = Path.TrimEndingDirectorySeparator(Settings.TargetPath);

            if (Settings.OperationType == "文件")
            {
                if (!File.Exists(targetPath))
                {
                    _logger.LogError("文件不存在: {Path}", targetPath);
                    throw new FileNotFoundException("文件不存在", targetPath);
                }

                try
                {
                    await Task.Run(() => File.Delete(targetPath));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "文件删除失败");
                    throw new Exception($"删除失败: {ex}");
                }

                _logger.LogInformation("文件删除成功: {Path}", targetPath);
            }
            else
            {
                // 文件夹分支走 cmd 的 rmdir，Windows 专属；文件分支只用 .NET 文件 API，全平台可用。
                if (!OperatingSystem.IsWindows())
                {
                    throw new PlatformNotSupportedException("文件夹删除依赖 cmd rmdir，仅在 Windows 上可用。");
                }

                if (!Directory.Exists(targetPath))
                {
                    _logger.LogError("文件夹不存在: {Path}", targetPath);
                    throw new DirectoryNotFoundException($"文件夹不存在: {targetPath}");
                }

                if (new DirectoryInfo(targetPath).Parent == null)
                {
                    throw new InvalidOperationException("请选择具体的文件夹，不能删除磁盘或共享根目录。");
                }

                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c rmdir /s /q \"{targetPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                _logger.LogInformation("执行命令: {Command}", psi.Arguments);

                using var process = Process.Start(psi) ?? throw new Exception("无法启动进程");
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    _logger.LogError("删除失败，退出码: {ExitCode}, 错误: {Error}", process.ExitCode, error);
                    throw new Exception($"删除失败: {error}");
                }

                _logger.LogInformation("文件夹删除成功: {Path}", targetPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除失败");
            throw;
        }

        _logger.LogDebug("DeleteAction OnInvoke 完成");
    }
}

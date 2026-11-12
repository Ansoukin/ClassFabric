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
[ActionInfo("SystemTools.Copy", "复制", FluentIcons.DocumentCopyRegular, false)]
public class CopyAction(ILogger<CopyAction> logger) : ActionBase<CopySettings>
{
    private readonly ILogger<CopyAction> _logger = logger;

    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        if (Settings == null || string.IsNullOrWhiteSpace(Settings.SourcePath) ||
            string.IsNullOrWhiteSpace(Settings.DestinationPath))
        {
            _logger.LogWarning("路径为空");
            return;
        }

        try
        {
            // 保留卷根（如 C:\）原样。裁成 C: 会变成驱动器相对路径，可能落到错误目录。
            var sourcePath = Path.TrimEndingDirectorySeparator(Settings.SourcePath);
            var destPath = Path.TrimEndingDirectorySeparator(Settings.DestinationPath);

            if (Settings.OperationType == "文件")
            {
                if (!File.Exists(sourcePath))
                {
                    _logger.LogError("源文件不存在: {Path}", sourcePath);
                    throw new FileNotFoundException("源文件不存在", sourcePath);
                }

                if (Directory.Exists(destPath))
                {
                    var fileName = Path.GetFileName(sourcePath);
                    destPath = Path.Combine(destPath, fileName);
                }

                var destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                try
                {
                    await Task.Run(() => File.Copy(sourcePath, destPath, true));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "文件复制失败");
                    throw new Exception($"复制失败: {ex}");
                }

                _logger.LogInformation("文件复制成功: {Source} -> {Destination}", sourcePath, destPath);
            }
            else
            {
                // 文件夹分支走 robocopy，Windows 专属；文件分支只用 .NET 文件 API，全平台可用。
                if (!OperatingSystem.IsWindows())
                {
                    throw new PlatformNotSupportedException("文件夹复制依赖 robocopy，仅在 Windows 上可用。");
                }

                if (!Directory.Exists(sourcePath))
                {
                    _logger.LogError("源文件夹不存在: {Path}", sourcePath);
                    throw new DirectoryNotFoundException($"源文件夹不存在: {sourcePath}");
                }

                var sourceDirectory = new DirectoryInfo(sourcePath);
                if (sourceDirectory.Parent == null)
                {
                    throw new InvalidOperationException("请选择具体的源文件夹，不能将磁盘或共享根目录作为源文件夹。");
                }

                var finalDestPath = Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(Path.Combine(destPath, sourceDirectory.Name)));
                var sourceFullPath = Path.TrimEndingDirectorySeparator(sourceDirectory.FullName);
                if (string.Equals(finalDestPath, sourceFullPath, StringComparison.OrdinalIgnoreCase)
                    || finalDestPath.StartsWith(sourceFullPath + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase)
                    || sourceFullPath.StartsWith(finalDestPath + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("目标文件夹不能与源文件夹相同，也不能相互包含。");
                }

                if (!Directory.Exists(destPath))
                {
                    Directory.CreateDirectory(destPath);
                }

                if (Directory.Exists(finalDestPath))
                {
                    Directory.Delete(finalDestPath, true);
                }

                // robocopy 退出码 < 8 都算成功（1~7 表示有文件被复制或跳过等正常情况）。
                var psi = new ProcessStartInfo
                {
                    FileName = "robocopy.exe",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                psi.ArgumentList.Add(sourcePath);
                psi.ArgumentList.Add(finalDestPath);
                psi.ArgumentList.Add("/e");
                psi.ArgumentList.Add("/copyall");
                psi.ArgumentList.Add("/r:3");
                psi.ArgumentList.Add("/w:3");
                psi.ArgumentList.Add("/mt:4");
                psi.ArgumentList.Add("/nfl");
                psi.ArgumentList.Add("/ndl");
                psi.ArgumentList.Add("/np");

                _logger.LogInformation("执行命令: robocopy \"{Source}\" \"{Destination}\"", sourcePath, finalDestPath);

                using var process = Process.Start(psi) ?? throw new Exception("无法启动进程");
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode >= 8)
                {
                    _logger.LogError("robocopy 失败，退出码: {ExitCode}, 输出: {Output}, 错误: {Error}",
                        process.ExitCode, output, error);
                    throw new Exception($"robocopy 失败，退出码: {process.ExitCode}");
                }

                _logger.LogInformation("文件夹复制成功: {Source} -> {Destination}", sourcePath, finalDestPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "复制失败");
            throw;
        }

        _logger.LogDebug("CopyAction OnInvoke 完成");
    }
}

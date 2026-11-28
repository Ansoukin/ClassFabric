using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using IoFile = System.IO.File;
using IoPath = System.IO.Path;
using ClassIsland.Assets.Localization.Main;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Controls;
using ClassIsland.Models.Actions;
using ClassIsland.Platforms.Abstraction;
using FluentAvalonia.UI.Controls;
using static ClassIsland.Models.Actions.RunActionSettings.RunActionRunType;
namespace ClassIsland.Services.Automation.Actions;

[AutomationCategory("应用与系统")]
[ActionInfo("classisland.os.run", "运行", "\uec2e", addDefaultToMenu:false)]
public class RunAction(IUriNavigationService uriNavigationService) : ActionBase<RunActionSettings>
{
    public IUriNavigationService UriNavigationService { get; } = uriNavigationService;

    /// <summary>识别命令因权限不足而失败的关键字（Windows 中文/英文系统、类 Unix 平台）。</summary>
    static readonly string[] AccessDeniedKeywords =
    [
        "拒绝访问",
        "Access is denied",
        "系统找不到指定的文件已被拒绝",
        "Permission denied",
        "requested operation requires elevation",
    ];

    protected override async Task OnInvoke()
    {
        await base.OnInvoke();
        switch (Settings.RunType)
        {
            case RunActionSettings.RunActionRunType.Application:
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Settings.Value,
                    Arguments = Settings.Args,
                    UseShellExecute = true
                });
                break;
            }
            case File:
            case Folder:
            {
                await PlatformServices.LauncherService.LaunchPath(Settings.Value);
                break;
            }
            case Url:
            {
                var path = Settings.Value;
                if (!string.IsNullOrWhiteSpace(path) && !path.Contains(':') && !path.StartsWith('\\'))
                    path = "https://" + path;

                if (OperatingSystem.IsWindows())
                {
                    Process.Start(new ProcessStartInfo(path)
                    {
                        UseShellExecute = true
                    });
                }
                else if (OperatingSystem.IsLinux())
                {
                    Process.Start(new ProcessStartInfo("xdg-open", path)
                    {
                        UseShellExecute = false
                    });
                }
                else if (OperatingSystem.IsMacOS())
                {
                    Process.Start(new ProcessStartInfo("open", path)
                    {
                        UseShellExecute = false
                    });
                }
                else
                {
                    UriNavigationService.NavigateWrapped(new Uri(path));
                }

                break;
            }
            case Command:
            {
                // 设置里已标记提权、或上次确认时记住授权的命令，直接走提权路径。
                if (Settings.IsRunAsAdmin || ActionItem.IsElevated)
                {
                    await RunCommandElevatedAsync(Settings.Value, InterruptCancellationToken);
                    return;
                }
                await RunCommandAsync(Settings.Value, Settings.IsRunAsAdmin, ActionItem, InterruptCancellationToken);
                break;
            }
            default:
                throw new NotSupportedException();
        }
    }


    const int MaxLength = 2000;
    async Task RunCommandAsync(string command, bool runAsAdmin, Shared.Models.Automation.ActionItem? actionItem,
        CancellationToken cancellationToken)
    {
        var (exitCode, stdout, stderr) = await RunCommandCoreAsync(command, runAsAdmin, cancellationToken);

        // 非管理员运行且输出是权限不足特征时，弹窗请用户确认后提权重跑一次。
        // 勾选"记住授权"则把置位写回随 Profile 持久化的设置，之后这条命令默认走管理员路径，
        // 行动项徽标同步点亮；不勾选则仅本次提权，下次仍会询问。
        // 不能依赖退出码：reg add 写 HKLM 被拒时输出"拒绝访问"但退出码为 0。
        if (!runAsAdmin && IsAccessDenied(stderr, stdout) && actionItem != null)
        {
            var (confirmed, remember) = await TryConfirmElevationAsync(command);
            if (!confirmed)
            {
                throw new InvalidOperationException(
                    $"命令执行失败 (退出代码: {exitCode})。该命令需要管理员权限。" + Environment.NewLine +
                    $"标准输出：{(stdout.Length == 0 ? "(空)" : stdout)}" + Environment.NewLine +
                    $"错误输出：{(stderr.Length == 0 ? "(空)" : stderr)}");
            }
            if (remember)
            {
                actionItem.IsElevated = true;
                Settings.IsRunAsAdmin = true;
            }
            (exitCode, stdout, stderr) = await RunCommandElevatedAsync(command, cancellationToken);
        }

        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"命令执行失败 (退出代码: {exitCode})。" + Environment.NewLine +
                $"标准输出：{(stdout.Length == 0 ? "(空)" : stdout)}" + Environment.NewLine +
                $"错误输出：{(stderr.Length == 0 ? "(空)" : stderr)}");
        }

        return;

        static bool IsAccessDenied(string stderr, string stdout) =>
            AccessDeniedKeywords.Any(k =>
                stderr.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                stdout.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 以管理员身份执行命令并回传真实退出码。UAC 跨令牌无法直接重定向输出，
    /// 借助 runas 提权 + 临时文件回传 stdout/stderr/退出码。
    /// </summary>
    static async Task<(int ExitCode, string Stdout, string Stderr)> RunCommandElevatedAsync(
        string command, CancellationToken cancellationToken)
    {
        var outputFile = IoPath.Combine(IoPath.GetTempPath(),
            $"classfabric-elev-{Guid.NewGuid():N}.txt");
        try
        {
            // %ERRORLEVEL% 是解析期展开，写在 & 链里取到的永远是提权 shell 的初始值 0 而非命令退出码，
            // 必须开延迟展开（cmd /v:on）后用 !ERRORLEVEL! 才能取到执行后的值；
            // 代价是命令文本里的 ! 会被 cmd 当作延迟变量边界吞掉，含 ! 的命令会失真。
            // Windows 之外用 $? 取退出码。输出统一写入临时文件再读回。
            var arguments = OperatingSystem.IsWindows()
                ? $"/v:on /c \"{command} > \"{outputFile}\" 2>&1 & echo @EXIT@!ERRORLEVEL!@EXIT@ >> \"{outputFile}\"\""
                : $"-c \"{command} > \"{outputFile}\" 2>&1; echo @EXIT@$? >> \"{outputFile}\"\"";
            try
            {
                Process.Start(new ProcessStartInfo(
                    OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash", arguments)
                {
                    CreateNoWindow = true,
                    UseShellExecute = true,
                    Verb = OperatingSystem.IsWindows() ? "runas" : null
                });
            }
            catch (Win32Exception e) when (OperatingSystem.IsWindows() && e.NativeErrorCode == 1223)
            {
                // 1223 = ERROR_CANCELLED，用户在 UAC 确认框点了取消。
                throw new OperationCanceledException("管理员权限请求已取消。");
            }

            // 提权进程无法托管等待（无句柄），轮询临时文件里的结束标记，同时响应中断。
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(500, cancellationToken);
                var parsed = ParseExitMarker(await ReadOutputAsync(outputFile));
                if (parsed.HasValue)
                {
                    return (parsed.Value.ExitCode, parsed.Value.Body, "");
                }
            }
        }
        finally
        {
            try { IoFile.Delete(outputFile); } catch { /* 清理失败无碍 */ }
        }

        // 从轮询输出里解析退出标记：取最后一个 @EXIT@，其前是命令输出，两个标记之间是退出码文本
        static (int ExitCode, string Body)? ParseExitMarker(string content)
        {
            var markerIndex = content.LastIndexOf("@EXIT@", StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                return null;
            }
            var body = content[..markerIndex];
            var tail = content[(markerIndex + "@EXIT@".Length)..];
            var endMarker = tail.IndexOf("@EXIT@", StringComparison.Ordinal);
            var exitCodeText = endMarker >= 0 ? tail[..endMarker].Trim() : tail.Trim();
            var exitCode = int.TryParse(exitCodeText, out var code) ? code : -1;
            return (exitCode, body);
        }

        static async Task<string> ReadOutputAsync(string file)
        {
            try
            {
                return await IoFile.ReadAllTextAsync(file, Encoding.UTF8);
            }
            catch (System.IO.IOException)
            {
                return ""; // 文件尚未创建或正被写入，下个轮询周期再试
            }
            catch (UnauthorizedAccessException)
            {
                return "";
            }
        }
    }

    /// <summary>以非提权方式执行命令并收集输出。</summary>
    static async Task<(int ExitCode, string Stdout, string Stderr)> RunCommandCoreAsync(
        string command, bool runAsAdmin, CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.EnableRaisingEvents = true;
        process.StartInfo = new ProcessStartInfo(
            OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash",
            OperatingSystem.IsWindows()
                ? $"/c \"{command}\""
                : $"-c \"{command.Replace("\"", "\\\"")}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        var stdoutBuilder = new StringBuilder(capacity: MaxLength);
        var stderrBuilder = new StringBuilder(capacity: MaxLength);

        process.OutputDataReceived += (s, e) => OnDataReceived(e, stdoutBuilder);
        process.ErrorDataReceived += (s, e) => OnDataReceived(e, stderrBuilder);

        if (!process.Start())
            throw new InvalidOperationException("无法启动进程。");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            throw new OperationCanceledException(
                $"命令执行被中断。" + Environment.NewLine +
                $"标准输出：{(stdoutBuilder.Length == 0 ? "(空)" : stdoutBuilder)}" + Environment.NewLine +
                $"错误输出：{(stderrBuilder.Length == 0 ? "(空)" : stderrBuilder)}");
        }

        return (process.ExitCode, stdoutBuilder.ToString(), stderrBuilder.ToString());

        static void OnDataReceived(DataReceivedEventArgs e, StringBuilder sb)
        {
            if (e.Data is null || sb.Length >= MaxLength)
                return;
            var remaining = MaxLength - sb.Length;
            if (e.Data.Length <= remaining)
            {
                if (sb.Length > 0)
                    sb.AppendLine();
                sb.Append(e.Data);
            }
            else
            {
                sb.Append(e.Data, 0, remaining);
                sb.AppendLine();
                sb.Append("…（已截断）");
            }
        }
    }

    /// <summary>
    /// 弹窗请用户确认是否以管理员身份重新运行该命令。
    /// 返回 Confirmed=是否提权重跑；Remember=是否记住授权（之后默认提权）。
    /// </summary>
    static async Task<(bool Confirmed, bool Remember)> TryConfirmElevationAsync(string command)
    {
        // 命令文本用只读展示块（Border+SelectableTextBlock）而不用 TextBox：
        // 输入框自带焦点边框和光标，视觉上像要编辑而不是要展示，且样式突兀。
        var commandBlock = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 8),
            Child = new ScrollViewer
            {
                MaxHeight = 110,
                Content = new SelectableTextBlock
                {
                    Text = command,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New, monospace"),
                    FontSize = 12.5
                }
            }
        };
        commandBlock[!Border.BackgroundProperty] = new DynamicResourceExtension("ControlFillColorDefaultBrush");
        commandBlock[!Border.BorderBrushProperty] = new DynamicResourceExtension("ControlStrokeColorDefaultBrush");
        commandBlock.BorderThickness = new Thickness(1);
        var rememberCheckBox = new CheckBox
        {
            Content = new TextBlock { Text = Localization.ElevationDialogRemember },
            IsChecked = true,
            Margin = new Thickness(0, 4, 0, 0)
        };
        var dialog = new FATaskDialog()
        {
            Header = Localization.ElevationDialogHeader,
            SubHeader = Localization.ElevationDialogMessage,
            Content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    commandBlock,
                    rememberCheckBox
                }
            },
            IconSource = new FluentIconSource("\uEF4F"),
            Buttons =
            {
                new FATaskDialogButton(Localization.ElevationDialogConfirm, true) { IsDefault = true },
                new FATaskDialogButton(Localization.ElevationDialogCancel, false)
            },
            XamlRoot = AppBase.Current.GetRootWindow()
        };
        var result = await dialog.ShowAsync();
        // ShowAsync 返回被点按钮的 result 对象（参照 SettingsWindowNew.axaml.cs 的 Equals(r, true) 用法），
        // 按按钮实例匹配会永远失配、走错"取消"分支。
        if (!Equals(result, true))
            return (false, false);
        return (true, rememberCheckBox.IsChecked == true);
    }
}

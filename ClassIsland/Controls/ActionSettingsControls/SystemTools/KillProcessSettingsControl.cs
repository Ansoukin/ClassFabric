using Avalonia;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Icons;
using ClassIsland.Models.Actions.SystemTools;

using Avalonia.Input.Platform;
namespace ClassIsland.Controls.ActionSettingsControls.SystemTools;

public class KillProcessSettingsControl : ActionSettingsControlBase<KillProcessSettings>
{
    private readonly TextBox _processNameBox;
    private readonly CheckBox _notifyCheckBox;

    public KillProcessSettingsControl()
    {
        var panel = new StackPanel { Spacing = 10, Margin = new(10) };

        panel.Children.Add(new TextBlock
        {
            Text = "退出进程",
            FontWeight = FontWeight.Bold,
            FontSize = 14
        });

        panel.Children.Add(new TextBlock
        {
            Text = "进程名:",
            Margin = new(0, 5, 0, 0)
        });

        _processNameBox = new TextBox
        {
            PlaceholderText = "输入进程名（如: notepad）"
        };
        panel.Children.Add(_processNameBox);

        var warningPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            Margin = new(0, 10, 0, 0)
        };

        // 插件这里用 Segoe Fluent Icons 的裸码点，主仓图标字体走 FluentIcons 常量 + Core 字体资源。
        warningPanel.Children.Add(new TextBlock
        {
            Text = FluentIcons.WarningRegular,
            FontFamily = new FontFamily("avares://ClassIsland.Core/Assets/Fonts/#FluentSystemIcons-Resizable"),
            FontSize = 16,
            Foreground = Brushes.Orange,
            VerticalAlignment = VerticalAlignment.Top
        });

        warningPanel.Children.Add(new TextBlock
        {
            Text = "警告：请勿终止系统重要进程 如 explorer.exe、System 等",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Orange,
            FontWeight = FontWeight.Medium
        });

        panel.Children.Add(warningPanel);

        var viewProcessesButton = new Button
        {
            Content = "查看正在运行的进程",
            Width = 200,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new(0, 10, 0, 0)
        };
        viewProcessesButton.Click += async (_, _) => await ShowProcessList();
        panel.Children.Add(viewProcessesButton);

        _notifyCheckBox = new CheckBox { Content = "当执行时发出提醒" };
        _notifyCheckBox.IsCheckedChanged += (_, _) => { Settings.NotifyOnExecute = _notifyCheckBox.IsChecked ?? false; };
        panel.Children.Add(_notifyCheckBox);

        Content = panel;
    }

    // 基类没有插件宿主里的 OnInitialized 钩子，改在挂到可视树时读取设置（此时 Settings 已就绪）。
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _notifyCheckBox.IsChecked = Settings.NotifyOnExecute;
        _processNameBox.Bind(TextBox.TextProperty, new Binding(nameof(Settings.ProcessName))
        {
            Source = Settings
        });
    }

    private async Task ShowProcessList()
    {
        // tasklist 输出的是 Windows 进程表，其他平台没有对应命令。
        if (!OperatingSystem.IsWindows())
        {
            await ShowErrorDialog("获取进程列表失败", "查看进程列表依赖 tasklist，仅在 Windows 上可用。");
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "tasklist",
                Arguments = "/fo table /nh",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(psi) ?? throw new Exception("无法启动 tasklist 进程");
            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                await ShowErrorDialog("获取进程列表失败", error);
                return;
            }

            await ShowProcessListWindow(output);
        }
        catch (Exception ex)
        {
            await ShowErrorDialog("获取进程列表失败", ex.Message);
        }
    }

    private async Task ShowErrorDialog(string title, string message)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {
            var window = new Window
            {
                Title = title,
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Spacing = 10,
                    Margin = new(10),
                    Children =
                    {
                        new TextBlock
                        {
                            Text = message,
                            TextWrapping = TextWrapping.Wrap
                        },
                        new Button
                        {
                            Content = "确定",
                            Width = 100,
                            HorizontalAlignment = HorizontalAlignment.Center
                        }
                    }
                }
            };
            await window.ShowDialog((Window)topLevel);
        }
    }

    private async Task ShowProcessListWindow(string processList)
    {
        var window = new Window
        {
            Title = "正在运行的进程",
            Width = 900,
            Height = 600,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var textBox = new TextBox
        {
            Text = processList,
            IsReadOnly = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Consolas, monospace"),
            FontSize = 12
        };

        ScrollViewer.SetVerticalScrollBarVisibility(textBox, ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollBarVisibility(textBox, ScrollBarVisibility.Auto);

        var copyButton = new Button
        {
            Content = "复制全部",
            Width = 100,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new(10)
        };
        copyButton.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is { } topLevel && topLevel.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(processList);
            }
        };

        var dockPanel = new DockPanel();
        DockPanel.SetDock(copyButton, Dock.Top);
        dockPanel.Children.Add(copyButton);
        dockPanel.Children.Add(textBox);

        window.Content = dockPanel;

        var parentTopLevel = TopLevel.GetTopLevel(this);
        if (parentTopLevel != null)
        {
            await window.ShowDialog((Window)parentTopLevel);
        }
    }
}

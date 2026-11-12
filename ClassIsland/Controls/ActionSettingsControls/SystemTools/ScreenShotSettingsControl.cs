using Avalonia;
using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Models.Actions.SystemTools;
using ClassIsland.Shared;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Controls.ActionSettingsControls.SystemTools;

public class ScreenShotSettingsControl : ActionSettingsControlBase<ScreenShotSettings>
{
    private readonly TextBox _folderPathBox;
    private readonly CheckBox _notifyCheckBox;

    public ScreenShotSettingsControl()
    {
        var panel = new StackPanel { Spacing = 10, Margin = new(10) };

        panel.Children.Add(new TextBlock
        {
            Text = "屏幕截图",
            FontWeight = FontWeight.Bold,
            FontSize = 14
        });

        var pathPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        _folderPathBox = new TextBox
        {
            PlaceholderText = "点击\"浏览...\"以选择保存文件夹",
            Width = 300,
            IsReadOnly = true
        };
        pathPanel.Children.Add(_folderPathBox);

        var browseButton = new Button
        {
            Content = "浏览...",
            Width = 80
        };
        browseButton.Click += async (_, _) => await BrowseFolder_Click();
        pathPanel.Children.Add(browseButton);

        panel.Children.Add(pathPanel);

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
        _folderPathBox.Text = Settings.SaveFolder;
    }

    private async Task BrowseFolder_Click()
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                var logger = IAppHost.TryGetService<ILogger<ScreenShotSettingsControl>>();
                logger?.LogWarning("无法获取 TopLevel");
                return;
            }

            var options = new FolderPickerOpenOptions
            {
                Title = "选择截图保存文件夹",
                AllowMultiple = false
            };

            var result = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
            if (result is { Count: > 0 })
            {
                Settings.SaveFolder = result[0].Path.LocalPath;
                _folderPathBox.Text = Settings.SaveFolder;
            }
        }
        catch (Exception ex)
        {
            var logger = IAppHost.TryGetService<ILogger<ScreenShotSettingsControl>>();
            logger?.LogError(ex, "选择保存文件夹失败");
        }
    }
}

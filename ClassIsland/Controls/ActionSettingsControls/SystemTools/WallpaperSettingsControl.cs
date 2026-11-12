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

public class WallpaperSettingsControl : ActionSettingsControlBase<WallpaperSettings>
{
    private readonly ComboBox _modeComboBox;
    private readonly CheckBox _notifyCheckBox;
    private readonly TextBlock _pathLabel;
    private readonly TextBox _pathBox;
    private readonly Button _browseButton;
    private readonly TextBlock _fitLabel;
    private readonly ComboBox _fitComboBox;
    private readonly TextBlock _solidColorLabel;
    private readonly TextBox _solidColorBox;

    public WallpaperSettingsControl()
    {
        var panel = new StackPanel { Spacing = 10, Margin = new(10) };

        panel.Children.Add(new TextBlock
        {
            Text = "壁纸类型:",
            FontWeight = FontWeight.Bold
        });

        _modeComboBox = new ComboBox
        {
            ItemsSource = new[] { "图片壁纸", "纯色壁纸" },
            Width = 200
        };
        _modeComboBox.SelectionChanged += (_, _) =>
        {
            if (_modeComboBox.SelectedIndex >= 0)
            {
                Settings.Mode = (ChangeWallpaperMode)_modeComboBox.SelectedIndex;
                RefreshModeVisibility();
            }
        };
        panel.Children.Add(_modeComboBox);

        _pathLabel = new TextBlock
        {
            Text = "图片路径:",
            FontWeight = FontWeight.Bold
        };
        panel.Children.Add(_pathLabel);

        _pathBox = new TextBox
        {
            PlaceholderText = "请选择壁纸图片文件"
        };
        _pathBox.TextChanged += (_, _) => { Settings.ImagePath = _pathBox.Text ?? ""; };
        panel.Children.Add(_pathBox);

        _browseButton = new Button
        {
            Content = "浏览...",
            Width = 100,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new(0, 5, 0, 0)
        };
        _browseButton.Click += async (_, _) => await BrowseButton_Click();
        panel.Children.Add(_browseButton);

        _fitLabel = new TextBlock
        {
            Text = "壁纸契合度:",
            FontWeight = FontWeight.Bold,
            Margin = new(0, 8, 0, 0)
        };
        panel.Children.Add(_fitLabel);

        // 顺序对应 FitStyle 0-5，与平台服务 SetWallpaperImage 的取值约定一致。
        _fitComboBox = new ComboBox
        {
            ItemsSource = new[] { "平铺", "居中", "拉伸", "填充", "适应", "跨区" },
            Width = 200
        };
        _fitComboBox.SelectionChanged += (_, _) =>
        {
            if (_fitComboBox.SelectedIndex >= 0)
                Settings.FitStyle = _fitComboBox.SelectedIndex;
        };
        panel.Children.Add(_fitComboBox);

        _solidColorLabel = new TextBlock
        {
            Text = "纯色颜色:",
            FontWeight = FontWeight.Bold,
            Margin = new(0, 8, 0, 0)
        };
        panel.Children.Add(_solidColorLabel);

        _solidColorBox = new TextBox
        {
            PlaceholderText = "#000000 或 0,0,0",
            Width = 200,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _solidColorBox.TextChanged += (_, _) => { Settings.SolidColor = _solidColorBox.Text ?? "#000000"; };
        panel.Children.Add(_solidColorBox);

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
        _modeComboBox.SelectedIndex = (int)Settings.Mode;
        _pathBox.Text = Settings.ImagePath;
        _fitComboBox.SelectedIndex = Settings.FitStyle;
        _solidColorBox.Text = Settings.SolidColor;
        RefreshModeVisibility();
    }

    private void RefreshModeVisibility()
    {
        var isImageMode = Settings.Mode == ChangeWallpaperMode.Image;
        _pathLabel.IsVisible = isImageMode;
        _pathBox.IsVisible = isImageMode;
        _browseButton.IsVisible = isImageMode;
        _fitLabel.IsVisible = isImageMode;
        _fitComboBox.IsVisible = isImageMode;
        _solidColorLabel.IsVisible = !isImageMode;
        _solidColorBox.IsVisible = !isImageMode;
    }

    private async Task BrowseButton_Click()
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                var logger = IAppHost.TryGetService<ILogger<WallpaperSettingsControl>>();
                logger?.LogWarning("无法获取 TopLevel");
                return;
            }

            var options = new FilePickerOpenOptions
            {
                Title = "选择壁纸图片",
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("图片文件")
                    {
                        Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif", "*.tiff", "*.webp" }
                    },
                    new FilePickerFileType("所有文件")
                    {
                        Patterns = new[] { "*" }
                    }
                }
            };

            var result = await topLevel.StorageProvider.OpenFilePickerAsync(options);
            if (result?.Count > 0)
            {
                var path = result[0].Path.LocalPath;
                Settings.ImagePath = path;
                _pathBox.Text = path;
            }
        }
        catch (Exception ex)
        {
            var logger = IAppHost.TryGetService<ILogger<WallpaperSettingsControl>>();
            logger?.LogError(ex, "选择壁纸文件失败");
        }
    }
}

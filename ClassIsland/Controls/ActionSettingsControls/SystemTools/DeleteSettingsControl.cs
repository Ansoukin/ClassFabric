using Avalonia;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Models.Actions.SystemTools;

namespace ClassIsland.Controls.ActionSettingsControls.SystemTools;

public class DeleteSettingsControl : ActionSettingsControlBase<DeleteSettings>
{
    private readonly ComboBox _typeComboBox;
    private readonly TextBox _targetPathBox;

    public DeleteSettingsControl()
    {
        var panel = new StackPanel { Spacing = 10, Margin = new(10) };

        panel.Children.Add(new TextBlock
        {
            Text = "删除文件或文件夹",
            FontWeight = FontWeight.Bold,
            FontSize = 14
        });

        var typePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        typePanel.Children.Add(new TextBlock
        {
            Text = "操作类型:",
            VerticalAlignment = VerticalAlignment.Center
        });
        _typeComboBox = new ComboBox
        {
            Width = 100,
            ItemsSource = new[] { "文件", "文件夹" }
        };
        _typeComboBox.SelectionChanged += (_, _) =>
        {
            Settings.OperationType = _typeComboBox.SelectedItem?.ToString() ?? "文件";
        };
        typePanel.Children.Add(_typeComboBox);
        panel.Children.Add(typePanel);

        panel.Children.Add(new TextBlock
        {
            Text = "目标路径:",
            FontWeight = FontWeight.Bold,
            Margin = new(0, 10, 0, 0)
        });
        _targetPathBox = new TextBox
        {
            PlaceholderText = "要删除的文件/文件夹路径"
        };
        _targetPathBox.TextChanged += (_, _) => Settings.TargetPath = _targetPathBox.Text ?? "";
        panel.Children.Add(_targetPathBox);
        var browseButton = new Button
        {
            Content = "浏览...",
            Width = 100,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        browseButton.Click += async (_, _) => await BrowsePath();
        panel.Children.Add(browseButton);

        Content = panel;
    }

    // 基类没有插件宿主里的 OnInitialized 钩子，改在挂到可视树时读取设置（此时 Settings 已就绪）。
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _typeComboBox.SelectedItem = Settings.OperationType;
        _targetPathBox.Text = Settings.TargetPath;
    }

    private async Task BrowsePath()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var isFolder = Settings.OperationType == "文件夹";

        if (isFolder)
        {
            var options = new FolderPickerOpenOptions
            {
                Title = "选择要删除的文件夹"
            };
            var result = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
            if (result?.Count > 0)
            {
                var uri = result[0].Path;
                var path = uri.IsAbsoluteUri ? uri.LocalPath : uri.OriginalString;
                Settings.TargetPath = path;
                _targetPathBox.Text = path;
            }
        }
        else
        {
            var options = new FilePickerOpenOptions
            {
                Title = "选择要删除的文件",
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("所有文件") { Patterns = new[] { "*" } }
                }
            };
            var result = await topLevel.StorageProvider.OpenFilePickerAsync(options);
            if (result?.Count > 0)
            {
                var path = result[0].Path.LocalPath;
                Settings.TargetPath = path;
                _targetPathBox.Text = path;
            }
        }
    }
}

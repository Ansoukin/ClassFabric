using Avalonia;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Models.Actions.SystemTools;

namespace ClassIsland.Controls.ActionSettingsControls.SystemTools;

public class MoveSettingsControl : ActionSettingsControlBase<MoveSettings>
{
    private readonly ComboBox _typeComboBox;
    private readonly TextBox _sourcePathBox;
    private readonly TextBox _destinationPathBox;

    public MoveSettingsControl()
    {
        var panel = new StackPanel { Spacing = 10, Margin = new(10) };

        panel.Children.Add(new TextBlock
        {
            Text = "移动文件或文件夹",
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
            Text = "从:",
            FontWeight = FontWeight.Bold,
            Margin = new(0, 10, 0, 0)
        });
        _sourcePathBox = new TextBox
        {
            PlaceholderText = "源文件/文件夹路径"
        };
        _sourcePathBox.TextChanged += (_, _) => Settings.SourcePath = _sourcePathBox.Text ?? "";
        panel.Children.Add(_sourcePathBox);
        var sourceBrowseButton = new Button
        {
            Content = "浏览...",
            Width = 100,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        sourceBrowseButton.Click += async (_, _) => await BrowsePath(true);
        panel.Children.Add(sourceBrowseButton);

        panel.Children.Add(new TextBlock
        {
            Text = "到:",
            FontWeight = FontWeight.Bold,
            Margin = new(0, 10, 0, 0)
        });
        _destinationPathBox = new TextBox
        {
            PlaceholderText = "目标文件夹路径"
        };
        _destinationPathBox.TextChanged += (_, _) => Settings.DestinationPath = _destinationPathBox.Text ?? "";
        panel.Children.Add(_destinationPathBox);
        var destBrowseButton = new Button
        {
            Content = "浏览...",
            Width = 100,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        destBrowseButton.Click += async (_, _) => await BrowsePath(false);
        panel.Children.Add(destBrowseButton);

        Content = panel;
    }

    // 基类没有插件宿主里的 OnInitialized 钩子，改在挂到可视树时读取设置（此时 Settings 已就绪）。
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _typeComboBox.SelectedItem = Settings.OperationType;
        _sourcePathBox.Text = Settings.SourcePath;
        _destinationPathBox.Text = Settings.DestinationPath;
    }

    private async Task BrowsePath(bool isSource)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        // 源路径跟随操作类型选文件还是文件夹；目标路径一律按文件夹处理。
        var isFolder = isSource ? Settings.OperationType == "文件夹" : true;

        if (isFolder)
        {
            var options = new FolderPickerOpenOptions
            {
                Title = isSource ? "选择源文件夹" : "选择目标文件夹"
            };
            var result = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
            if (result?.Count > 0)
            {
                var uri = result[0].Path;
                var path = uri.IsAbsoluteUri ? uri.LocalPath : uri.OriginalString;
                if (isSource)
                {
                    Settings.SourcePath = path;
                    _sourcePathBox.Text = path;
                }
                else
                {
                    Settings.DestinationPath = path;
                    _destinationPathBox.Text = path;
                }
            }
        }
        else
        {
            var options = new FilePickerOpenOptions
            {
                Title = "选择源文件",
                FileTypeFilter = new[] { new FilePickerFileType("所有文件") { Patterns = new[] { "*" } } }
            };
            var result = await topLevel.StorageProvider.OpenFilePickerAsync(options);
            if (result?.Count > 0)
            {
                var path = result[0].Path.LocalPath;
                Settings.SourcePath = path;
                _sourcePathBox.Text = path;
            }
        }
    }
}

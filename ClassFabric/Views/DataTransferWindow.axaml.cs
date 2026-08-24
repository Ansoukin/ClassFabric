using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using ClassFabric.Core.Controls;
using ClassFabric.Enums;

namespace ClassFabric.Views;

public partial class DataTransferWindow : MyWindow
{
    public int ImportStage { init; get; } = -1;
    
    public string? ImportName { get; init; }

    private DataTransferPage _dataTransferPage;
    
    
    public DataTransferWindow()
    {
        InitializeComponent();
        Content = _dataTransferPage = new DataTransferPage();
        if (OperatingSystem.IsMacOS())
        {
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.PreferSystemChrome;
            ExtendClientAreaTitleBarHeightHint = -1;
            SystemDecorations = SystemDecorations.Full;
        }
    }

    public async Task PerformClassFabricImport(string root, ImportEntries importEntries)
    {
#pragma warning disable CS0612 // 类型或成员已过时
        await _dataTransferPage.PerformClassFabricImport(root, importEntries);
#pragma warning restore CS0612 // 类型或成员已过时
    }
    
    public async Task PerformClassFabric2Import(string root, ImportEntries importEntries)
    {
        await _dataTransferPage.PerformClassFabric2Import(root, importEntries);
    }

    private void Control_OnLoaded(object? sender, RoutedEventArgs e)
    {
        
    }

    public void ImportComplete(bool importV1)
    {
        _dataTransferPage.ViewModel.PageIndex = 4;
        _dataTransferPage.ImportComplete(importV1);
    }
}
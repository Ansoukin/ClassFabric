using Avalonia.Interactivity;
using ClassFabric.RemoteBroadcast.Services;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Enums.SettingsWindow;
using ClassIsland.Core.Icons;

namespace ClassFabric.RemoteBroadcast.UI;

/// <summary>
/// 远程广播「呼叫记录」页：只读最近 20 条，内容全在内存里。
/// </summary>
[SettingsPageInfo("remoteBroadcast.history", "呼叫记录", FluentIcons.HistoryRegular, FluentIcons.HistoryRegular,
    SettingsPageCategory.External)]
[Group("remoteBroadcast")]
public partial class RemoteBroadcastHistorySettingsPage : SettingsPageBase
{
    private readonly QuickCallService _quickCall;

    public RemoteBroadcastHistorySettingsPage(QuickCallService quickCall)
    {
        _quickCall = quickCall;
        InitializeComponent();
        Loaded += (_, _) => RefreshHistory();
    }

    private void RefreshHistory()
    {
        HistoryList.ItemsSource = _quickCall.GetHistory()
            .Select(r => new HistoryRow(r))
            .ToList();
    }

    private void RefreshHistory_Click(object? sender, RoutedEventArgs e)
    {
        RefreshHistory();
    }
}

/// <summary>呼叫记录行视图模型。</summary>
public class HistoryRow(CallRecord record)
{
    public string TimeDisplay { get; } = record.AtUtc.ToLocalTime().ToString("MM-dd HH:mm:ss");
    public string Caller { get; } = record.Caller;
    public string StudentsDisplay { get; } = string.Join("、", record.Students);
    public string Destination { get; } = record.Destination;
    public string Result { get; } = record.Result;
}

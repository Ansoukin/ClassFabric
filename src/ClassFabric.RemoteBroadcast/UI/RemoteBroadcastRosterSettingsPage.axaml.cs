using Avalonia.Controls;
using Avalonia.Interactivity;
using ClassFabric.RemoteBroadcast.Models;
using ClassFabric.RemoteBroadcast.Services;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Enums.SettingsWindow;
using ClassIsland.Core.Icons;
using Microsoft.Extensions.Logging;
using QRCoder;

namespace ClassFabric.RemoteBroadcast.UI;

/// <summary>
/// 远程广播「人员与目的地」页：接入码、学生名单、预设目的地。
/// 这三块都是「白板上一次性备好的数据」，跟服务/播报参数不是一回事，所以从主设置页拆出来单独一页。
/// </summary>
[SettingsPageInfo("remoteBroadcast.roster", "人员与目的地", FluentIcons.PeopleTeamRegular, FluentIcons.PeopleTeamRegular,
    SettingsPageCategory.External)]
[Group("remoteBroadcast")]
public partial class RemoteBroadcastRosterSettingsPage : SettingsPageBase
{
    private readonly ConfigStore _store;
    private readonly AccessCodeManager _codes;
    private readonly ILogger<RemoteBroadcastRosterSettingsPage> _logger;

    private DateTime _clearConfirmUntil = DateTime.MinValue;

    public RemoteBroadcastRosterSettingsPage(
        ConfigStore store, AccessCodeManager codes, ILogger<RemoteBroadcastRosterSettingsPage> logger)
    {
        _store = store;
        _codes = codes;
        _logger = logger;
        InitializeComponent();
        Loaded += (_, _) => LoadFromConfig();
    }

    private void LoadFromConfig()
    {
        RefreshCodes();
        RefreshRoster();
        RefreshDestinations();
    }

    // ---- 接入码 ----

    private void RefreshCodes()
    {
        CodeList.ItemsSource = _codes.GetAll()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new CodeRow(x))
            .ToList();
    }

    private void CreateCode_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            _codes.Create(NewCodeNoteBox.Text ?? "");
            NewCodeNoteBox.Text = "";
            CodeMsgText.Text = "";
            RefreshCodes();
        }
        catch (ArgumentException ex)
        {
            // 备注为空/超长是老师最容易踩的一步，光记日志等于点了没反应，把原因摆到列表上方。
            CodeMsgText.Text = ex.Message;
            _logger.LogWarning("[RemoteBroadcast] 创建接入码被拒：{Message}", ex.Message);
        }
    }

    private void RevokeCode_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is CodeRow row)
        {
            _codes.Revoke(row.Token);
            RefreshCodes();
        }
    }

    private void ShowQr_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not CodeRow row)
        {
            return;
        }

        var config = _store.Snapshot();
        var ip = LanUtils.PickPreferredAddress(LanUtils.GetLanAddresses(), config.PreferredLanAddress);
        if (ip == null)
        {
            // 这句原本落在主设置页的服务状态行上；拆页之后二维码按钮在人员页，提示得跟着按钮走，
            // 否则老师点了二维码什么也看不到，还得翻回另一页找原因。
            CodeMsgText.Text = "未找到可用的局域网 IPv4 地址，无法生成二维码。请检查网络连接。";
            return;
        }

        var url = $"http://{ip}:{Math.Clamp(config.Port, 1024, 65535)}/{row.Token}/";
        var owner = TopLevel.GetTopLevel(this) as Window;
        var qrWindow = new QrWindow(row.Note, url);
        if (owner != null)
        {
            qrWindow.ShowDialog(owner);
        }
        else
        {
            qrWindow.Show();
        }
    }

    // ---- 学生名单 ----

    private void RefreshRoster()
    {
        var roster = _store.Snapshot().Roster;
        RosterList.ItemsSource = roster.OrderBy(x => x.Group).ThenBy(x => x.Name).ToList();
        RosterCountText.Text = $"共 {roster.Count} 人（上限 500）";
    }

    private void AddStudent_Click(object? sender, RoutedEventArgs e)
    {
        var name = NewStudentNameBox.Text?.Trim() ?? "";
        if (name.Length == 0 || name.Length > 20)
        {
            RosterMsgText.Text = "姓名不能为空且不超过 20 字符";
            return;
        }

        var group = NewStudentGroupBox.Text?.Trim() ?? "";
        if (_store.Snapshot().Roster.Count >= 500)
        {
            RosterMsgText.Text = "名单已达 500 人上限";
            return;
        }

        _store.Update(c => c.Roster.Add(new StudentEntry { Name = name, Group = group }));
        NewStudentNameBox.Text = "";
        NewStudentGroupBox.Text = "";
        RosterMsgText.Text = "";
        RefreshRoster();
    }

    private void ImportStudents_Click(object? sender, RoutedEventArgs e)
    {
        // 一行一名，「姓名,分组」可选。容错原则：非法行跳过并提示数量，别因为一两行脏数据把整批导入掀翻。
        var lines = BatchImportBox.Text?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        var added = 0;
        var skipped = 0;
        _store.Update(c =>
        {
            foreach (var line in lines)
            {
                var parts = line.Split(',', 2);
                var name = parts[0].Trim();
                if (name.Length == 0 || name.Length > 20 || c.Roster.Count >= 500)
                {
                    skipped++;
                    continue;
                }

                c.Roster.Add(new StudentEntry
                {
                    Name = name,
                    Group = parts.Length > 1 ? parts[1].Trim() : "",
                });
                added++;
            }
        });
        BatchImportBox.Text = "";
        RosterMsgText.Text = skipped > 0 ? $"导入 {added} 人，跳过 {skipped} 行（空名/超长/超上限）" : $"已导入 {added} 人";
        RefreshRoster();
    }

    private void ClearRoster_Click(object? sender, RoutedEventArgs e)
    {
        // 清空全名单是危险操作，用「二次点击确认」替代弹窗：5 秒内再点一次才真删。
        if (DateTime.Now > _clearConfirmUntil)
        {
            _clearConfirmUntil = DateTime.Now.AddSeconds(5);
            ClearRosterLabel.Text = "再点一次确认清空";
            return;
        }

        _clearConfirmUntil = DateTime.MinValue;
        ClearRosterLabel.Text = "清空全部";
        _store.Update(c => c.Roster.Clear());
        RefreshRoster();
    }

    private void DeleteStudent_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is StudentEntry entry)
        {
            _store.Update(c => c.Roster.RemoveAll(x => x.Id == entry.Id));
            RefreshRoster();
        }
    }

    // ---- 目的地预设 ----

    private void RefreshDestinations()
    {
        DestinationList.ItemsSource = _store.Snapshot().Destinations.ToList();
    }

    private void AddDestination_Click(object? sender, RoutedEventArgs e)
    {
        var name = NewDestinationBox.Text?.Trim() ?? "";
        if (name.Length == 0 || name.Length > 50)
        {
            return;
        }

        _store.Update(c => c.Destinations.Add(new DestinationEntry { Name = name }));
        NewDestinationBox.Text = "";
        RefreshDestinations();
    }

    private void DeleteDestination_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is DestinationEntry entry)
        {
            _store.Update(c => c.Destinations.RemoveAll(x => x.Id == entry.Id));
            RefreshDestinations();
        }
    }
}

/// <summary>接入码列表行视图模型：把 UTC 时间和绑定状态翻成人话。</summary>
public class CodeRow(AccessCodeEntry entry)
{
    public string Token { get; } = entry.Token;
    public string TokenShort { get; } = entry.Token[..8] + "…";
    public string Note { get; } = entry.Note;
    public string BoundDisplay { get; } = string.IsNullOrEmpty(entry.BoundTeacher) ? "未绑定" : entry.BoundTeacher;
    public string BoundAtDisplay { get; } = entry.BoundAtUtc is { } t ? "绑定于 " + t.ToLocalTime().ToString("MM-dd HH:mm") : "";
    public string CreatedAtDisplay { get; } = entry.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
}

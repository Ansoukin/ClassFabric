using Avalonia.Interactivity;
using ClassFabric.RemoteBroadcast.Models;
using ClassFabric.RemoteBroadcast.Services;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Enums.SettingsWindow;
using ClassIsland.Core.Icons;
using FluentAvalonia.UI.Controls;

namespace ClassFabric.RemoteBroadcast.UI;

/// <summary>
/// 远程广播「主设置」页：服务开关/端口、播报参数与模板。
/// 播报参数改动即保存（配置颗粒度小，拆「保存按钮」反而容易让人怀疑没存上）；
/// 只有端口和服务开关例外——它们要重启监听，放在「应用并重启服务」里一起做。
/// </summary>
[SettingsPageInfo("remoteBroadcast", "主设置", FluentIcons.SettingsRegular, FluentIcons.SettingsRegular,
    SettingsPageCategory.External)]
[Group("remoteBroadcast")]
public partial class RemoteBroadcastSettingsPage : SettingsPageBase
{
    private readonly ConfigStore _store;
    private readonly BroadcastHttpServer _server;

    public RemoteBroadcastSettingsPage(ConfigStore store, BroadcastHttpServer server)
    {
        _store = store;
        _server = server;
        InitializeComponent();
        // 拨到「自定义」才放开数值输入，跟随开关走，免得出现「开关关着改数值却没生效」的困惑。
        CustomWidthToggle.IsCheckedChanged += (_, _) => CustomWidthBox.IsEnabled = CustomWidthToggle.IsChecked == true;
        Loaded += (_, _) => LoadFromConfig();
    }

    private void LoadFromConfig()
    {
        var c = _store.Snapshot();
        ServiceToggle.IsChecked = c.ServiceEnabled;
        PortBox.Value = Math.Clamp(c.Port, 1024, 65535);
        LoopBox.Value = Math.Clamp(c.AnnouncementLoopCount, 1, 5);
        HintToggle.IsChecked = c.SpeechHintEnabled;
        VolumeBox.Value = Math.Clamp(c.AnnouncementVolume, 0f, 100f);
        VoiceBox.Text = c.VoiceName;
        CustomWidthToggle.IsChecked = c.IsCustomOverlayWidthEnabled;
        CustomWidthBox.Value = Math.Clamp(c.CustomOverlayWidth, 200, 4000);
        CustomWidthBox.IsEnabled = c.IsCustomOverlayWidthEnabled;
        TemplateSingleBox.Text = c.TemplateSingle;
        TemplateGuideBox.Text = c.TemplateMultiGuide;
        TemplateHintBox.Text = c.TemplateSpeechHint;
        RefreshServiceStatus();
        RefreshAddresses();
    }

    // ---- 服务 ----

    private void RefreshServiceStatus()
    {
        var ip = LanUtils.PickPreferredAddress(LanUtils.GetLanAddresses(), _store.Snapshot().PreferredLanAddress);
        var port = ReadInt(PortBox, 5212, 1024, 65535);
        ServiceStatusText.Text = _server.BindMode switch
        {
            "Lan" => $"服务状态：运行中，局域网可访问 http://{ip}:{port}/",
            "LocalOnly" => $"服务状态：降级运行（仅本机可访问）。{_server.BindHint}",
            "Stopped" when _server.BindHint != null => $"服务状态：未运行。{_server.BindHint}",
            _ => "服务状态：未运行（可在上方开关启用）",
        };
    }

    private void RefreshAddresses()
    {
        var c = _store.Snapshot();
        var addresses = LanUtils.GetLanAddresses();
        AddressBox.ItemsSource = addresses.Select(a => $"{a.Ip}（{a.AdapterName}）").ToList();
        var picked = addresses.FindIndex(a => a.Ip == c.PreferredLanAddress);
        AddressBox.SelectedIndex = picked >= 0 ? picked : 0;
    }

    private void ApplyService_Click(object? sender, RoutedEventArgs e)
    {
        var ip = AddressBox.SelectedItem as string ?? "";
        // ComboBox 文案是「IP（网卡名）」拼接，取前半段还原纯 IP。
        var chosenIp = ip.Split('（')[0];
        _store.Update(c =>
        {
            c.ServiceEnabled = ServiceToggle.IsChecked == true;
            c.Port = ReadInt(PortBox, 5212, 1024, 65535);
            c.PreferredLanAddress = chosenIp;
        });
        _server.Restart();
        RefreshServiceStatus();
    }

    // ---- 播报参数与模板 ----

    private void SaveAnnounceSettings_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            // 模板先校验再入库：非法占位符直接拒绝并提示，别让一条坏模板把播报链路带崩在运行时。
            AnnouncementComposer.ValidateTemplate(TemplateSingleBox.Text ?? "", "单学生模板");
            AnnouncementComposer.ValidateTemplate(TemplateGuideBox.Text ?? "", "多学生引导句");
            AnnouncementComposer.ValidateTemplate(TemplateHintBox.Text ?? "", "语音提示模板");
        }
        catch (AnnouncementException ex)
        {
            AnnounceMsgText.Text = ex.Message;
            return;
        }

        _store.Update(c =>
        {
            c.AnnouncementLoopCount = ReadInt(LoopBox, 2, 1, 5);
            c.SpeechHintEnabled = HintToggle.IsChecked == true;
            c.AnnouncementVolume = (float)ReadDouble(VolumeBox, 80, 0, 100);
            c.VoiceName = (VoiceBox.Text ?? "").Trim();
            c.IsCustomOverlayWidthEnabled = CustomWidthToggle.IsChecked == true;
            c.CustomOverlayWidth = ReadDouble(CustomWidthBox, 800, 200, 4000);
            c.TemplateSingle = TemplateSingleBox.Text ?? "";
            c.TemplateMultiGuide = TemplateGuideBox.Text ?? "";
            c.TemplateSpeechHint = TemplateHintBox.Text ?? "";
        });
        AnnounceMsgText.Text = "已保存";
    }

    // ---- 小工具 ----

    /// <summary>
    /// FANumberBox 的 Value 是 double，输入框被清空或填了非法内容时会变成 NaN（宿主靠 NotNaNConverter
    /// 挡这个）。这里是命令式读值，不挡的话 (int)NaN 会静默变成 0，端口直接从 1024 起步，看着像配置被吃了。
    /// 统一按「非法/越界 → 收敛回合法区间，NaN → 回退默认值」处理。
    /// </summary>
    private static int ReadInt(FANumberBox box, int fallback, int min, int max)
    {
        return double.IsFinite(box.Value) ? (int)Math.Clamp(box.Value, min, max) : fallback;
    }

    private static double ReadDouble(FANumberBox box, double fallback, double min, double max)
    {
        return double.IsFinite(box.Value) ? Math.Clamp(box.Value, min, max) : fallback;
    }
}

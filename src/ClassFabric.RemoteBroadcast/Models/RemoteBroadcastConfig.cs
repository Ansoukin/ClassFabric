namespace ClassFabric.RemoteBroadcast.Models;

/// <summary>
/// 插件持久化配置（config.json）。字段默认值即任务书 v2.3 规定值。
/// </summary>
public class RemoteBroadcastConfig
{
    /// <summary>R1：HTTP 服务开关。</summary>
    public bool ServiceEnabled { get; set; } = true;

    /// <summary>R1：监听端口（1024–65535）。</summary>
    public int Port { get; set; } = 5212;

    /// <summary>R6 选配：播报前语音提示「{Caller} 正在呼叫」（默认关）。</summary>
    public bool SpeechHintEnabled { get; set; }

    /// <summary>R6：语音提示模板（默认随任务书措辞，含 {Caller} 占位符）。</summary>
    public string TemplateSpeechHint { get; set; } = "{Caller}正在呼叫。";

    /// <summary>4.3：播报循环次数（1–5，默认 2）。</summary>
    public int AnnouncementLoopCount { get; set; } = 2;

    /// <summary>播报音量（0–100）。</summary>
    public float AnnouncementVolume { get; set; } = 80f;

    /// <summary>EdgeTTS 音色名。</summary>
    public string VoiceName { get; set; } = "zh-CN-XiaoxiaoNeural";

    /// <summary>
    /// 播报条宽度：true = 用下面的自定义固定宽度；false = 跟随播报前岛条原始宽度（不低于屏幕宽度 20%）。
    /// </summary>
    public bool IsCustomOverlayWidthEnabled { get; set; }

    /// <summary>自定义播报条宽度（逻辑像素，仅 IsCustomOverlayWidthEnabled=true 时生效）。</summary>
    public double CustomOverlayWidth { get; set; } = 800;

    /// <summary>4.3：单学生播报模板（默认模板常量，任务书允许内置）。</summary>
    public string TemplateSingle { get; set; } = "请{Students}到{Destination}找{Caller}。";

    /// <summary>4.3：多学生引导句模板（含 {Caller}）。</summary>
    public string TemplateMultiGuide { get; set; } = "请以下学生到{Destination}来找{Caller}。";

    /// <summary>R4：学生名单（≤500）。</summary>
    public List<StudentEntry> Roster { get; set; } = [];

    /// <summary>R5：预设目的地（默认五项）。</summary>
    public List<DestinationEntry> Destinations { get; set; } =
    [
        new DestinationEntry { Name = "办公室" },
        new DestinationEntry { Name = "实验室" },
        new DestinationEntry { Name = "医务室" },
        new DestinationEntry { Name = "德育处" },
        new DestinationEntry { Name = "门卫处" },
    ];

    /// <summary>R2：接入码列表（撤销即移除）。</summary>
    public List<AccessCodeEntry> AccessCodes { get; set; } = [];

    /// <summary>R1：生成二维码时优先使用的网卡 IP（空 = 自动选取）。</summary>
    public string PreferredLanAddress { get; set; } = "";
}

public class StudentEntry
{
    /// <summary>短随机 id（名单条目稳定标识，手机端按 id 选人）。</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];

    public string Name { get; set; } = "";

    /// <summary>可选分组（R4）。</summary>
    public string Group { get; set; } = "";
}

public class DestinationEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];

    public string Name { get; set; } = "";
}

public class AccessCodeEntry
{
    /// <summary>R2：32 位随机 hex 接入码。</summary>
    public string Token { get; set; } = "";

    /// <summary>创建时必填备注（如「刘老师」）。</summary>
    public string Note { get; set; } = "";

    /// <summary>v2.2：登记后绑定的教师姓名（重新登记覆盖）。</summary>
    public string BoundTeacher { get; set; } = "";

    public DateTime? BoundAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

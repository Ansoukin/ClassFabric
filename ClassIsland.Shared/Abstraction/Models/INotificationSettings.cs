namespace ClassIsland.Shared.Abstraction.Models;

/// <summary>
/// 提醒提供方设置接口
/// </summary>
public interface INotificationSettings
{
    /// <summary>
    /// 是否启用提醒
    /// </summary>
    public bool IsNotificationEnabled
    {
        get;
        set;
    }

    /// <summary>
    /// 是否启用语音
    /// </summary>
    public bool IsSpeechEnabled
    {
        get;
        set;
    }

    /// <summary>
    /// 是否启用提醒效果
    /// </summary>
    public bool IsNotificationEffectEnabled
    {
        get;
        set;
    }

    /// <summary>
    /// 是否启用提醒音效
    /// </summary>
    public bool IsNotificationSoundEnabled
    {
        get;
        set;
    }

    /// <summary>
    /// 提醒音效路径
    /// </summary>
    public string NotificationSoundPath
    {
        get;
        set;
    }

    /// <summary>
    /// 是否在提醒时置顶主界面
    /// </summary>
    public bool IsNotificationTopmostEnabled
    {
        get;
        set;
    }
    
    /// <summary>
    /// 提醒效果 ID
    /// </summary>
    public string NotificationEffectId { get; set; }

    /// <summary>
    /// 提醒特效风格：0 = 官方特效（默认），1 = 涟漪特效（FancyAnima，仅 Windows）。
    /// 播放端判断涟漪是否可用，不可用时自动回落官方特效。
    /// </summary>
    public int NotificationEffectStyle { get; set; }
}

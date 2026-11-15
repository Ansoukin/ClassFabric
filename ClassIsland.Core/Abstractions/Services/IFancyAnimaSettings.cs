namespace ClassIsland.Core.Abstractions.Services;

/// <summary>
/// 涟漪特效（FancyAnima）的动画精度档位。越精细画面越细腻，占用也越高。
/// </summary>
public enum AnimationQuality
{
    /// <summary>
    /// 简化：动画时长减半，关键帧简化，最低占用。
    /// </summary>
    Simplified,
    /// <summary>
    /// 标准：默认动画参数，平衡流畅度与性能。
    /// </summary>
    Standard,
    /// <summary>
    /// 精细：动画时长延长，更细腻的缓动，最佳视觉效果。
    /// </summary>
    Fine
}

/// <summary>
/// 涟漪特效设置契约。特效设置只存在于插件工程（net10.0，不随主界面多目标编译），
/// 「主界面-窗口」设置页经此接口读写，不直接引用插件程序集。
/// 非平台注册此服务时不可用，界面按 null 隐藏对应设置组。
/// </summary>
public interface IFancyAnimaSettings : System.ComponentModel.INotifyPropertyChanged
{
    /// <summary>
    /// 是否启用涟漪特效。
    /// </summary>
    bool IsRippleEffectEnabled { get; set; }

    /// <summary>
    /// 帧率限制：30 或 60。
    /// </summary>
    int RippleEffectFps { get; set; }

    /// <summary>
    /// 动画精度档位。
    /// </summary>
    AnimationQuality RippleEffectQuality { get; set; }

    /// <summary>
    /// 手动强制播放：官方特效开启时也播放涟漪（官方特效关闭是自动让位的默认行为）。
    /// </summary>
    bool ManualOverride { get; set; }
}

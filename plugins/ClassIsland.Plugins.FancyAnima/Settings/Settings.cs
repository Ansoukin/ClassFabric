using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using ClassIsland.Core.Abstractions.Services;

namespace FancyAnima.Settings;

/// <summary>
/// 插件配置模型 —— 实现 INotifyPropertyChanged 以支持自动保存。
/// JSON 键与社区版保持一致，两版配置互不干扰但内容格式相同。
/// </summary>
public class Settings : INotifyPropertyChanged, IFancyAnimaSettings
{
    private bool _enableHighPerformanceAnimation = true;
    private int _fpsLimit = 60;
    private AnimationQuality _quality = AnimationQuality.Standard;
    private bool _firstRunComplete = false;
    private bool _manualOverrideOfficialEffectsDisabled = false;

    /// <summary>是否启用高性能合成动画</summary>
    [JsonPropertyName("enableHighPerformanceAnimation")]
    public bool EnableHighPerformanceAnimation
    {
        get => _enableHighPerformanceAnimation;
        set => SetField(ref _enableHighPerformanceAnimation, value);
    }

    /// <summary>帧率限制：30 或 60</summary>
    [JsonPropertyName("fpsLimit")]
    public int FpsLimit
    {
        get => _fpsLimit;
        set => SetField(ref _fpsLimit, value);
    }

    /// <summary>动画精度等级</summary>
    [JsonPropertyName("quality")]
    public AnimationQuality Quality
    {
        get => _quality;
        set => SetField(ref _quality, value);
    }

    /// <summary>是否已完成首次运行引导（内置版不再有引导页，仅保留配置兼容）</summary>
    [JsonPropertyName("firstRunComplete")]
    public bool FirstRunComplete
    {
        get => _firstRunComplete;
        set => SetField(ref _firstRunComplete, value);
    }

    /// <summary>
    /// 手动覆盖：用户告知插件"官方特效已关闭"。
    /// 当自动检测失效时，用户可手动开启此选项。
    /// </summary>
    [JsonPropertyName("manualOverrideOfficialEffectsDisabled")]
    public bool ManualOverrideOfficialEffectsDisabled
    {
        get => _manualOverrideOfficialEffectsDisabled;
        set => SetField(ref _manualOverrideOfficialEffectsDisabled, value);
    }

    // ============================================================
    //  IFancyAnimaSettings（主界面-窗口设置页绑定入口）
    //  不能用显式接口实现：绑定按属性名查找并监听对应 PropertyChanged，
    //  这里做成同一字段的别名视图，赋值走同一个 SetField。
    // ============================================================

    /// <inheritdoc />
    public bool IsRippleEffectEnabled
    {
        get => _enableHighPerformanceAnimation;
        set => SetField(ref _enableHighPerformanceAnimation, value);
    }

    /// <inheritdoc />
    public int RippleEffectFps
    {
        get => _fpsLimit;
        set => SetField(ref _fpsLimit, value);
    }

    /// <inheritdoc />
    public AnimationQuality RippleEffectQuality
    {
        get => _quality;
        set => SetField(ref _quality, value);
    }

    /// <inheritdoc />
    public bool ManualOverride
    {
        get => _manualOverrideOfficialEffectsDisabled;
        set => SetField(ref _manualOverrideOfficialEffectsDisabled, value);
    }

    // ============================================================
    //  INotifyPropertyChanged
    // ============================================================

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

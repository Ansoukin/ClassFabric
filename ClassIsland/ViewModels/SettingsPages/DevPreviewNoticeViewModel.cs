using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.ViewModels.SettingsPages;

/// <summary>
/// 开发者预览版提示窗的选项状态：下拉里选中某个「不再显示」后，
/// 主按钮切换为纯「确定」，再点一下就按所选语义关窗。
/// </summary>
public class DevPreviewNoticeViewModel : ObservableObject
{
    private bool _suppressOnceChosen;
    private bool _suppressPermanentChosen;

    public bool SuppressOnceChosen
    {
        get => _suppressOnceChosen;
        set => SetProperty(ref _suppressOnceChosen, value);
    }

    public bool SuppressPermanentChosen
    {
        get => _suppressPermanentChosen;
        set => SetProperty(ref _suppressPermanentChosen, value);
    }
}

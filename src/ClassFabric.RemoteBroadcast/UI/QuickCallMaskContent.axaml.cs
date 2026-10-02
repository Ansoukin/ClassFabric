using Avalonia.Controls;
using ClassIsland.Core.Helpers.UI;

namespace ClassFabric.RemoteBroadcast.UI;

/// <summary>
/// Quick Call 遮罩内容：「{Caller} 正在呼叫」。内容构造时定死（一次播报一份实例），不搞绑定。
/// </summary>
public partial class QuickCallMaskContent : UserControl
{
    public QuickCallMaskContent()
    {
        InitializeComponent();
    }

    /// <param name="minWidth">要求通知药丸至少这么宽（逻辑像素），0 = 不约束。</param>
    public QuickCallMaskContent(string text, double minWidth) : this()
    {
        MaskText.Text = text;
        // 图标表达式与宿主 CreateTwoIconsMask 的默认参数一字不差（info / bell 两个 lucide 图标）——
        // 不是拍脑袋挑的码点，是照抄宿主，保证自绘 Mask 和宿主模板肉眼无差。
        LeftIcon.IconSource = IconExpressionHelper.TryParseOrNull("lucide(\ue0ff)");
        RightIcon.IconSource = IconExpressionHelper.TryParseOrNull("lucide(\ue224)");
        if (minWidth > 0)
        {
            MinWidth = minWidth;
        }
    }
}

using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using ClassIsland.Core.Controls.NotificationTemplates;
using ClassIsland.Core.Models.Notification.Templates;

namespace ClassFabric.RemoteBroadcast.UI;

/// <summary>
/// Quick Call 播报正文卡片：只渲染「请{Students}到{Destination}…」这一行正文（视听同步，R6）。
/// 呼叫方那句「{Caller} 正在呼叫」归 Mask 阶段显示，这里不重复——不然两行叠进 40px 高的岛里就溢出了。
/// 内容在构造时定死（一次播报一份实例），所以不搞绑定，直接塞控件——少一层绑定就少一类运行时惊喜。
/// </summary>
public partial class QuickCallOverlayContent : UserControl
{
    private readonly string _bodyText = "";
    private readonly TimeSpan _scrollDuration;
    private readonly int _scrollRepeatCount;
    private bool _measurementDone;

    public QuickCallOverlayContent()
    {
        InitializeComponent();
    }

    /// <param name="scrollDuration">滚动字幕一轮总时长，传 Overlay 的显示时长，让字幕正好滚到画面收起。</param>
    /// <param name="scrollRepeatCount">滚动重复次数，对齐播报循环次数——念几遍就滚几趟。</param>
    /// <param name="minWidth">要求岛条至少这么宽（逻辑像素），0 = 不约束。</param>
    public QuickCallOverlayContent(string bodyText, TimeSpan scrollDuration, int scrollRepeatCount, double minWidth = 0) : this()
    {
        _bodyText = bodyText;
        _scrollDuration = scrollDuration;
        _scrollRepeatCount = scrollRepeatCount;
        BodyText.Text = bodyText;
        // 宽度挂在 RootPanel 而不是 TextBlock：切滚动字幕时 Children 要整个换血，
        // 挂在面板上，静态/滚动两种形态才能吃到同一个最小宽度。
        if (minWidth > 0)
        {
            RootPanel.MinWidth = minWidth;
        }
        // 文本放不放得下要等第一次布局量出可用宽度才知道，量完再决定要不要换滚动字幕。
        BodyText.LayoutUpdated += OnBodyTextLayoutUpdated;
    }

    private void OnBodyTextLayoutUpdated(object? sender, EventArgs e)
    {
        if (_measurementDone)
        {
            return;
        }

        var available = BodyText.Bounds.Width;
        if (available <= 0)
        {
            // 还没排到实际尺寸，下一帧再说。
            return;
        }

        _measurementDone = true;
        BodyText.LayoutUpdated -= OnBodyTextLayoutUpdated;

        // 按当前字体量文本的自然宽度：放得下就保持静态单行，放不下（比如 30 人名单）才切滚动字幕。
        var typeface = new Typeface(BodyText.FontFamily, BodyText.FontStyle, BodyText.FontWeight);
        var naturalWidth = new TextLayout(_bodyText, typeface, BodyText.FontSize).Width;
        if (naturalWidth <= available + 0.5)
        {
            return;
        }

        // 复用宿主自己的滚动文本控件：动画、裁剪、循环都由它负责，这里只把时长和次数对齐播报。代价是
        // 它的字号写死在模板里（17），和静态正文的 MainWindowLargeFontSize 不完全一致——长名单属于少数场景，
        // 为这点差异去抄一份滚动实现不划算。
        RootPanel.Children.Clear();
        RootPanel.Children.Add(new RollingTextTemplate(new RollingTextTemplateData
        {
            Text = _bodyText,
            Duration = _scrollDuration,
            RepeatCount = _scrollRepeatCount,
        }));
    }
}

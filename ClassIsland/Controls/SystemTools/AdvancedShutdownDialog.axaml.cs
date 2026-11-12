using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace ClassIsland.Controls.SystemTools;

/// <summary>
/// 高级计时关机的倒计时对话框。计划进行期间不允许用户手动关闭，
/// 最小化会立刻还原——这个窗口的使命就是让人无法「顺眼一闭就当没看见」。
/// 各控件句柄由 AdvancedShutdownAction 通过下列属性取用。
/// </summary>
public partial class AdvancedShutdownDialog : Window
{
    public AdvancedShutdownDialog()
    {
        InitializeComponent();

        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty && WindowState == WindowState.Minimized)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    WindowState = WindowState.Normal;
                    Activate();
                    InvalidateVisual();
                }, DispatcherPriority.MaxValue);
            }
        };
    }

    public TextBlock? CountdownTextBlock => CountdownTextBlockElement;
    public ProgressBar? CountdownProgressBar => CountdownProgressBarElement;
    public Button? ImmediateShutdownButton => ImmediateShutdownButtonElement;
    public Button? ReadButton => ReadButtonElement;
    public Button? CancelPlanButton => CancelPlanButtonElement;
    public Button? ExtendButton => ExtendButtonElement;
}

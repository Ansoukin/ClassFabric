using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ClassIsland.Controls.SystemTools;

/// <summary>
/// 延长高级计时关机倒计时的输入对话框，确认后以 ResultMinutes 回传分钟数。
/// </summary>
public partial class ExtendShutdownDialog : Window
{
    public int? ResultMinutes { get; private set; }

    public ExtendShutdownDialog()
    {
        InitializeComponent();

        if (ConfirmButton is not null)
        {
            ConfirmButton.Click += OnConfirmButtonClick;
        }

        if (CancelButton is not null)
        {
            CancelButton.Click += OnCancelButtonClick;
        }

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

    public NumericUpDown? MinutesInput => MinutesInputElement;
    public Button? ConfirmButton => ConfirmButtonElement;
    public Button? CancelButton => CancelButtonElement;

    private void OnConfirmButtonClick(object? sender, RoutedEventArgs e)
    {
        ResultMinutes = (int)(MinutesInput?.Value ?? 1);
        Close();
    }

    private void OnCancelButtonClick(object? sender, RoutedEventArgs e)
    {
        ResultMinutes = null;
        Close();
    }
}

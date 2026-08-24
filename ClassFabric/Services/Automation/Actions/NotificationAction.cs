using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ClassFabric.Core.Abstractions.Automation;
using ClassFabric.Core.Attributes;
using ClassFabric.Core.Models.Notification;
using ClassFabric.Helpers;
using ClassFabric.Models.Actions;
using ClassFabric.Services.NotificationProviders;
using ClassFabric.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
namespace ClassFabric.Services.Automation.Actions;

[ActionInfo("classfabric.showNotification", "显示提醒", "\ue02b", addDefaultToMenu:false)]
public class NotificationAction : ActionBase<NotificationActionSettings>
{
    static ActionNotificationProvider ActionNotificationProvider { get; } =
        IAppHost.Host.Services.GetServices<IHostedService>().OfType<ActionNotificationProvider>().First();

    protected override async Task OnInvoke()
    {
        await base.OnInvoke();
        var notificationTask = ShowNotificationAsync(Settings);

        if (!Settings.IsWaitForCompleteEnabled) return;

        using var cts = new CancellationTokenSource();
        PropertyChangedEventHandler propertyChangedHandler = null;

        try
        {
            Settings.PropertyChanged += PropertyChangedHandler;

            var completedTask = await Task.WhenAny(
                notificationTask,
                Task.Delay(Timeout.Infinite, cts.Token)
            ).ConfigureAwait(false);

            if (completedTask == notificationTask)
                await notificationTask.ConfigureAwait(false);
        }
        catch (TaskCanceledException) when (cts.IsCancellationRequested) { }
        finally
        {
            Settings.PropertyChanged -= PropertyChangedHandler;
            cts.Cancel();
        }
        return;

        void PropertyChangedHandler(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.IsWaitForCompleteEnabled) && !Settings.IsWaitForCompleteEnabled)
                cts.Cancel();
        }
    }


    protected override async Task OnInterrupted()
    {
        await base.OnInterrupted();
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            _cancellationTokenSource?.Cancel();
            _completedTokenSource?.Cancel();
        });
    }



    CancellationTokenSource? _cancellationTokenSource;
    CancellationTokenSource? _completedTokenSource;

    async Task ShowNotificationAsync(NotificationActionSettings settings)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var request = new NotificationRequest
            {
                MaskContent = NotificationContent.CreateTwoIconsMask(settings.Mask, hasRightIcon: false, factory: x =>
                {
                    x.Duration = TimeSpanHelper.FromSecondsSafe(settings.MaskDurationSeconds);
                    x.IsSpeechEnabled = settings.IsMaskSpeechEnabled;
                }),
                OverlayContent = string.IsNullOrEmpty(settings.Content) || settings.ContentDurationSeconds <= 0
                    ? null
                    : NotificationContent.CreateSimpleTextContent(settings.Content, factory: x =>
                    {
                        x.IsSpeechEnabled = settings.IsContentSpeechEnabled;
                        x.Duration = TimeSpanHelper.FromSecondsSafe(settings.ContentDurationSeconds);
                    }),
                RequestNotificationSettings =
                {
                    IsSettingsEnabled = settings.IsAdvancedSettingsEnabled,
                    IsNotificationEffectEnabled = settings.IsEffectEnabled,
                    IsNotificationSoundEnabled = settings.IsSoundEffectEnabled,
                    IsNotificationTopmostEnabled = settings.IsTopmostEnabled,
                    NotificationSoundPath = settings.CustomSoundEffectPath,
                    IsSpeechEnabled = true
                }
            };
            _cancellationTokenSource = request.CancellationTokenSource;
            _completedTokenSource = request.CompletedTokenSource;
            await ActionNotificationProvider.ShowNotificationAsync(request);
        });
    }
}
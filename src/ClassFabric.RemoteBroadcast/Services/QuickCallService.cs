using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ClassFabric.RemoteBroadcast.Models;
using ClassFabric.RemoteBroadcast.UI;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Models.Notification;
using Microsoft.Extensions.Logging;

namespace ClassFabric.RemoteBroadcast.Services;

/// <summary>有播报正在进行，新呼叫被拒（映射 HTTP 409）。</summary>
public class BroadcastBusyException : Exception;

public enum BroadcastState
{
    Idle,
    Broadcasting,
    Error,
}

/// <summary>R7：呼叫记录（内存环形 20 条，重启即清、不落盘——隐私红线）。</summary>
public sealed record CallRecord(DateTime AtUtc, List<string> Students, string Destination, string Caller, string Result);

/// <summary>/api/status 的 current 段。</summary>
public sealed record CurrentBroadcast(string Caller, List<string> Students, string Destination, DateTime StartedAtUtc);

/// <summary>
/// Quick Call 状态机：同一时刻只允许一个播报任务（R8）。
/// 一条完整链路 = 先合成 → 通知先行（Mask 3s → Overlay 与语音同起）→ Overlay 按语音时长自然收起。
/// 失败不外抛给 HTTP（那时早已 202），而是写进状态机，让手机端轮询 /api/status 拿到可读错误（验收 5）。
/// </summary>
public class QuickCallService(
    ConfigStore store,
    EdgeTtsClient tts,
    IAudioService audio,
    QuickCallNotificationProvider provider,
    ILogger<QuickCallService> logger)
{
    private const int HistoryCapacity = 20;
    private static readonly TimeSpan MaskDuration = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ErrorDisplayWindow = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan OverlayHardCap = TimeSpan.FromSeconds(90);

    private readonly object _lock = new();
    private BroadcastState _state = BroadcastState.Idle;
    private CurrentBroadcast? _current;
    private string _lastError = "";
    private DateTime _errorSinceUtc;
    private readonly List<CallRecord> _history = [];
    private CancellationTokenSource? _playbackCts;

    /// <summary>
    /// 受理一次呼叫：参数校验与播报文本构造（失败=400）→ 忙碌检查（失败=409）→
    /// 置为播报中并立刻返回，后续链路在后台跑。
    /// </summary>
    public AnnouncementResult AcceptCall(string caller, List<string> studentNames, string destination)
    {
        var config = store.Snapshot();
        var announcement = AnnouncementComposer.Compose(
            studentNames, destination, caller, config.TemplateSingle, config.TemplateMultiGuide,
            config.AnnouncementLoopCount);

        lock (_lock)
        {
            if (_state == BroadcastState.Broadcasting)
            {
                throw new BroadcastBusyException();
            }

            _state = BroadcastState.Broadcasting;
            _current = new CurrentBroadcast(caller, studentNames, destination, DateTime.UtcNow);
            _lastError = "";
        }

        _ = RunAnnouncementAsync(announcement, caller, studentNames, destination);
        return announcement;
    }

    private async Task RunAnnouncementAsync(
        AnnouncementResult announcement, string caller, List<string> students, string destination)
    {
        var config = store.Snapshot();

        // 总保险丝：哪怕音频设备卡死，95 秒后状态机也必须归位，否则整个插件从此 409 到天荒地老。
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(95));
        _playbackCts = cts;
        NotificationRequest? request = null;
        try
        {
            // 顺序是「先合成、再上屏」：合成失败时白板根本没闪过任何东西，错误只走手机端，
            // 不存在「画面上来了、声音没来」的半残状态。顺带把语音时长量出来，喂给 Overlay 的 Duration。
            var segments = new List<Stream>();
            if (config.SpeechHintEnabled)
            {
                segments.Add(await tts.SynthesizeAsync(
                    AnnouncementComposer.FillHint(config.TemplateSpeechHint, caller), config.VoiceName, cts.Token));
            }

            segments.Add(await tts.SynthesizeAsync(announcement.SpeechText, config.VoiceName, cts.Token));

            // 48kbps 的 MP3 每秒约 6000 字节，拿字节数反推时长就够用了——反正是给 Overlay 收起留余量的。
            var speechSeconds = segments.Sum(s => s.CanSeek ? s.Length : 0) / 6000.0;
            var overlayDuration = TimeSpan.FromSeconds(Math.Min(speechSeconds + 2, OverlayHardCap.TotalSeconds));

            // 宿主的规矩（NotificationWorkerService）：CompletedToken 只在 Overlay 自然播满 Duration 时才触发。
            // 所以「收起」不能靠主动 Cancel——Cancel 只杀画面，CompletedToken 永远不来，ShowNotificationAsync
            // 的等待就永远挂着，状态机卡死在 Broadcasting（这个坑真实踩过一轮 E2E 才看清）。把 Duration 算准，
            // 让宿主自己收尾才是干净路数。
            // 宽度决策也放这个 UI 线程 lambda 里：此刻岛条还停在播报前的正常状态（组件全摊在上面），
            // 量出来的就是「原始宽度」；而且视觉树只有 UI 线程能碰。
            request = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var barWidth = ResolveOverlayBarWidth(config);
                return new NotificationRequest
                {
                    // Mask 不走宿主的 CreateTwoIconsMask：那个模板不给宽度入口，「正在呼叫」这 3 秒要是不跟着
                    // 加宽，正文一上来岛条突然变宽，肉眼可见地跳一下。QuickCallMaskContent 外观照抄宿主模板，
                    // 只多一个最小宽度。
                    MaskContent = new NotificationContent
                    {
                        Content = new QuickCallMaskContent($"{caller} 正在呼叫", barWidth),
                        SpeechContent = $"{caller} 正在呼叫",
                        Duration = MaskDuration,
                    },
                    OverlayContent = new NotificationContent
                    {
                        Content = new QuickCallOverlayContent(
                            announcement.VisualText, overlayDuration, config.AnnouncementLoopCount, barWidth),
                        Duration = overlayDuration,
                    },
                };
            });
            var showTask = provider.ShowNotificationAsync(request);

            // 语音等 Mask 那 3 秒走完再开口，画面和声音一起上。（队列繁忙时 Mask 可能晚于这 3 秒才轮到，
            // 会有些许错位——M1 不追这个，真实教室里白板不会同时被别的提醒抢。）
            await Task.Delay(MaskDuration, cts.Token);
            foreach (var segment in segments)
            {
                await audio.PlayAudioAsync(segment, config.AnnouncementVolume, cts.Token);
                segment.Dispose();
            }

            // Overlay 播满 Duration 后宿主触发 CompletedToken，这里才会返回。保险丝兜住宿主侧的意外
            // （比如提醒被别的通知反复打断重排），超时也得让状态机归位，绝不能 409 到天荒地老。
            await showTask.WaitAsync(cts.Token);

            AddHistory(students, destination, caller, "已完成");
            ResetIfBroadcasting();
            logger.LogInformation("[RemoteBroadcast] 播报完成（{Caller} → {Destination}，{Count} 人）", caller, destination, students.Count);
        }
        catch (SpeechSynthesisException e)
        {
            // 合成发生在上屏之前，走到这里 request 必然还是 null，不涉及画面收尾。
            AddHistory(students, destination, caller, "失败：" + e.Message);
            SetError(e.Message);
            logger.LogError("[RemoteBroadcast] 语音合成失败：{Message}", e.Message);
        }
        catch (OperationCanceledException)
        {
            // 停用或保险丝触发：立即把画面收掉是第一位的。代价是 CompletedToken 永远不来、
            // ShowNotificationAsync 的等待任务挂在后台出不来——中止路径上这点泄漏比挂着假通知强。
            TryCancelRequest(request);
            AddHistory(students, destination, caller, "已取消");
            ResetIfBroadcasting();
            logger.LogInformation("[RemoteBroadcast] 播报被取消");
        }
        catch (Exception e)
        {
            // 内部错误只给手机端一句人话，细节留日志——堆栈发到老师手机上没有任何意义。
            // 画面不主动收：让它按 Duration 自然走完，宿主才能干净地完成 CompletedToken，不留悬挂任务。
            AddHistory(students, destination, caller, "失败：内部错误");
            SetError("播报过程中出现内部错误，请查看白板日志");
            logger.LogError(e, "[RemoteBroadcast] 播报链路异常");
        }
        finally
        {
            _playbackCts = null;
        }
    }

    /// <summary>/api/status：Error 状态挂 30 秒供手机端轮询到，过期自动回 Idle，避免卡死在错误态。</summary>
    public (BroadcastState State, CurrentBroadcast? Current, string LastError) GetStatus()
    {
        lock (_lock)
        {
            if (_state == BroadcastState.Error && DateTime.UtcNow - _errorSinceUtc > ErrorDisplayWindow)
            {
                _state = BroadcastState.Idle;
                _lastError = "";
            }

            return (_state, _current, _lastError);
        }
    }

    /// <summary>/api/history：最新在前，最多 20 条。</summary>
    public List<CallRecord> GetHistory()
    {
        lock (_lock)
        {
            return _history.Take(HistoryCapacity).ToList();
        }
    }

    /// <summary>插件停用时掐断当前播报，端口释放由 HTTP 服务自己负责。</summary>
    public void Stop()
    {
        _playbackCts?.Cancel();
    }

    private void AddHistory(List<string> students, string destination, string caller, string result)
    {
        lock (_lock)
        {
            _history.Insert(0, new CallRecord(DateTime.UtcNow, students, destination, caller, result));
            if (_history.Count > HistoryCapacity)
            {
                _history.RemoveRange(HistoryCapacity, _history.Count - HistoryCapacity);
            }
        }
    }

    private void SetError(string message)
    {
        lock (_lock)
        {
            _state = BroadcastState.Error;
            _lastError = message;
            _errorSinceUtc = DateTime.UtcNow;
            _current = null;
        }
    }

    private void ResetIfBroadcasting()
    {
        lock (_lock)
        {
            if (_state == BroadcastState.Broadcasting)
            {
                _state = BroadcastState.Idle;
                _current = null;
            }
        }
    }

    private void TryCancelRequest(NotificationRequest? request)
    {
        try
        {
            request?.Cancel();
        }
        catch (Exception e)
        {
            // 请求可能已经被宿主收尾了，取消扑空无所谓，别让它在收尾路径上炸出新的异常。
            logger.LogDebug(e, "[RemoteBroadcast] 取消提醒请求失败（可忽略）");
        }
    }

    /// <summary>
    /// 算播报条的最小宽度（逻辑像素）。默认跟随播报前岛条的原始宽度——主线放了课表就直接用课表宽，
    /// 老师们熟悉的那条多宽，播报条就多宽；再拿屏幕宽度 20% 兜底，无组件/窄组件的裸机不至于塌缩成
    /// 一小截，高分屏上比例也撑得住。设置里开了自定义宽度就完全按配置来，不再叠兜底。
    /// 极端情况下拿不到主窗口就返回 0（不约束，退回宿主「内容多宽条多宽」的默认行为）。
    /// 只能在 UI 线程调用（要碰视觉树）。
    /// </summary>
    private static double ResolveOverlayBarWidth(RemoteBroadcastConfig config)
    {
        if (config.IsCustomOverlayWidthEnabled)
        {
            // 自定义也保个下限，手滑填个位数的话正文连遮罩都盖不住，那不是配置是事故。
            return Math.Max(200, config.CustomOverlayWidth);
        }

        var window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (window == null)
        {
            return 0;
        }

        // 插件不引用宿主主程序集，点不了 MainWindowLine 的名，只能按类型名在视觉树里找岛条，
        // 再反射读它的 BackgroundWidth（岛条背景宽 = 主线组件的实际宽度，组件播报期间照常参与测量，
        // 此时读到的就是「播报前原始值」）。宿主哪天改了类型名，这里会静默拿不到宽度——
        // 退化成屏宽兜底而不是崩掉，属于可接受的降级。
        double islandWidth = 0;
        var line = window.GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(x => x.GetType().Name == "MainWindowLine");
        if (line?.GetType().GetProperty("BackgroundWidth")?.GetValue(line) is double w)
        {
            islandWidth = w;
        }

        // 主窗口被宿主钉成全屏宽（窗口 Width = 屏幕宽），ClientSize 就是屏幕的逻辑宽度。
        var screenFloor = window.ClientSize.Width * 0.2;
        return Math.Max(islandWidth, screenFloor);
    }
}

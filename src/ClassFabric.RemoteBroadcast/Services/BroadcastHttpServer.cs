using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using ClassFabric.RemoteBroadcast.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClassFabric.RemoteBroadcast.Services;

/// <summary>
/// Quick Call 手机端 HTTP 服务（R1–R5、R8、R10–R11 的服务端半边）。
/// 所有业务路由都在 /&lt;接入码&gt;/ 前缀下，无效接入码一律 404——不暴露「这里有个服务」的存在感（R2）。
/// </summary>
public class BroadcastHttpServer(
    ConfigStore store,
    AccessCodeManager codes,
    QuickCallService quickCall,
    string pluginFolder,
    string version,
    ILogger<BroadcastHttpServer> logger) : IHostedService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly ConcurrentDictionary<string, (DateTime Start, int Count)> _rateLimits = new();
    private readonly object _listenerLock = new();
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;

    /// <summary>绑定模式：Lan（局域网可访问）/ LocalOnly（降级，仅本机）/ Stopped。</summary>
    public string BindMode { get; private set; } = "Stopped";

    /// <summary>绑定失败时的人话说明（设置页展示，含下一步指引）。</summary>
    public string? BindHint { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var config = store.Snapshot();
        if (config.ServiceEnabled)
        {
            StartCore(config.Port);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        StopCore();
        return Task.CompletedTask;
    }

    /// <summary>设置页改端口/开关后调用：整体重启一次监听。</summary>
    public void Restart()
    {
        StopCore();
        var config = store.Snapshot();
        if (config.ServiceEnabled)
        {
            StartCore(config.Port);
        }
        else
        {
            BindMode = "Stopped";
        }
    }

    private void StartCore(int port)
    {
        lock (_listenerLock)
        {
            StopCore();
            port = Math.Clamp(port, 1024, 65535);
            _listener = new HttpListener();
            _cts = new CancellationTokenSource();

            try
            {
                // 全地址绑定需要一次性的 urlacl 授权（管理员执行一次即可）：
                //   netsh http add urlacl url=http://+:{端口}/ user=Everyone
                // 没授权就会 AccessDenied——这是 Windows HTTP.sys 的规矩，不是 bug，别想着绕。
                _listener.Prefixes.Add($"http://+:{port}/");
                _listener.Start();
                BindMode = "Lan";
                BindHint = null;
                logger.LogInformation("[RemoteBroadcast] HTTP 服务已监听 http://+:{Port}/（局域网可访问）", port);
            }
            catch (HttpListenerException e) when (e.ErrorCode is 5 or 1015)
            {
                // 5 = AccessDenied：典型原因是没配 urlacl。降级到 localhost 保住「本机能调通」的验证能力，
                // 手机端则要等 urlacl 配好——提示里把命令原样给出来，照抄就行。
                BindHint = $"局域网监听失败（需要一次性 urlacl 授权）。请以管理员执行：netsh http add urlacl url=http://+:{port}/ user=Everyone 后重启服务。当前已降级为仅本机可访问。";
                logger.LogWarning("[RemoteBroadcast] {Hint}（ErrorCode={Code}）", BindHint, e.ErrorCode);
                try
                {
                    _listener = new HttpListener();
                    _listener.Prefixes.Add($"http://localhost:{port}/");
                    _listener.Start();
                    BindMode = "LocalOnly";
                }
                catch (Exception inner)
                {
                    BindMode = "Stopped";
                    logger.LogError(inner, "[RemoteBroadcast] 降级监听 localhost 也失败");
                }
            }
            catch (HttpListenerException e)
            {
                // R1：端口被占/冲突必须给明确错误和修改指引，不许静默失败。
                BindMode = "Stopped";
                BindHint = $"端口 {port} 监听失败（{e.Message}）。请在设置页换一个端口（1024–65535）后重启服务。";
                logger.LogError("[RemoteBroadcast] {Hint}", BindHint);
                _listener = null;
            }

            if (_listener?.IsListening == true)
            {
                _ = AcceptLoopAsync(_cts!.Token);
            }
        }
    }

    private void StopCore()
    {
        lock (_listenerLock)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            if (_listener != null)
            {
                try
                {
                    _listener.Stop();
                    _listener.Close();
                }
                catch (Exception e)
                {
                    logger.LogDebug(e, "[RemoteBroadcast] 关闭监听时的收尾异常（可忽略）");
                }
            }

            _listener = null;
            BindMode = "Stopped";
        }
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        // GetContextAsync 在 Stop 时会抛 ObjectDisposed/异常，这里当作正常退出信号处理。
        while (!token.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener!.GetContextAsync();
            }
            catch (Exception) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (HttpListenerException e)
            {
                logger.LogWarning("[RemoteBroadcast] Accept 异常（{Code}），继续接受下一个连接", e.ErrorCode);
                continue;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await HandleAsync(ctx);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "[RemoteBroadcast] 处理请求时出现未捕获异常（{Path}）", ctx.Request.Url?.AbsolutePath);
                    TryWriteJson(ctx, 500, new { error = "服务器内部错误" });
                }
            }, CancellationToken.None);
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url?.AbsolutePath ?? "/";
        var response = ctx.Response;
        response.Headers["Cache-Control"] = "no-store";

        // 路由第一刀：切出接入码。码不对直接 404，后面的 api/静态都不会存在。
        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || codes.TryGet(segments[0]) == null)
        {
            TryWriteJson(ctx, 404, new { error = "Not Found" });
            return;
        }

        var subPath = "/" + string.Join("/", segments.Skip(1));
        switch (subPath)
        {
            case "/" or "/index.html":
                ServeSpa(ctx);
                return;
            case "/api/register" when ctx.Request.HttpMethod == "POST":
                await HandleRegisterAsync(ctx);
                return;
            case "/api/config":
                HandleConfig(ctx);
                return;
            case "/api/quickcall" when ctx.Request.HttpMethod == "POST":
                await HandleQuickCallAsync(ctx);
                return;
            case "/api/status":
                HandleStatus(ctx);
                return;
            case "/api/history":
                HandleHistory(ctx);
                return;
            default:
                TryWriteJson(ctx, 404, new { error = "Not Found" });
                return;
        }
    }

    // ---- 各端点实现 ----

    private async Task HandleRegisterAsync(HttpListenerContext ctx)
    {
        var body = await ReadJsonBodyAsync<RegisterRequest>(ctx);
        if (body == null)
        {
            return;
        }

        // R10：姓名简称 2–20 字符。首尾空格是手滑，帮忙去掉；去掉后不达标就是真不达标。
        var name = body.TeacherName?.Trim() ?? "";
        if (name.Length is < 2 or > 20)
        {
            TryWriteJson(ctx, 400, new { error = "姓名简称需为 2–20 个字符" });
            return;
        }

        var code = GetCodeSegment(ctx)!;
        if (!codes.Bind(code, name))
        {
            // 理论上走不到（路由层已验过码），但万一在两步之间码被撤销了，还是按 404 处理干净。
            TryWriteJson(ctx, 404, new { error = "Not Found" });
            return;
        }

        logger.LogInformation("[RemoteBroadcast] 教师登记绑定：{Name} → 接入码 {Code}…", name, code[..8]);
        TryWriteJson(ctx, 200, new { bound = true });
    }

    private void HandleConfig(HttpListenerContext ctx)
    {
        var config = store.Snapshot();
        TryWriteJson(ctx, 200, new
        {
            roster = config.Roster.Select(s => new { id = s.Id, name = s.Name, group = s.Group }),
            destinations = config.Destinations.Select(d => new { id = d.Id, name = d.Name }),
            version,
        });
    }

    private async Task HandleQuickCallAsync(HttpListenerContext ctx)
    {
        // R8：同 IP 对呼叫接口限速 2 次/秒，超限 429——防手抖连点，也防有人的「自动化」脚本。
        var ip = ctx.Request.RemoteEndPoint?.Address.ToString() ?? "";
        if (!AllowRequest(ip))
        {
            TryWriteJson(ctx, 429, new { error = "请求太频繁，请稍候再试" });
            return;
        }

        var body = await ReadJsonBodyAsync<QuickCallRequest>(ctx);
        if (body == null)
        {
            return;
        }

        var caller = (body.Caller ?? "").Trim();
        var destination = (body.Destination ?? "").Trim();
        if (caller.Length is < 2 or > 20)
        {
            TryWriteJson(ctx, 400, new { error = "呼叫人姓名需为 2–20 个字符" });
            return;
        }

        if (destination.Length is < 1 or > 50)
        {
            TryWriteJson(ctx, 400, new { error = "目的地需为 1–50 个字符" });
            return;
        }

        // 手机端送来的是学生 id（来自 /api/config），这里翻译成名字；翻译不出来的按参数错误处理。
        var rosterMap = store.Snapshot().Roster.ToDictionary(s => s.Id, s => s.Name);
        var studentNames = new List<string>();
        foreach (var id in body.Students ?? [])
        {
            if (!rosterMap.TryGetValue(id ?? "", out var name))
            {
                TryWriteJson(ctx, 400, new { error = "名单中包含未知学生，请刷新页面后重试" });
                return;
            }

            studentNames.Add(name);
        }

        if (studentNames.Count == 0)
        {
            TryWriteJson(ctx, 400, new { error = "请至少选择一名学生" });
            return;
        }

        try
        {
            quickCall.AcceptCall(caller, studentNames, destination);
            TryWriteJson(ctx, 202, new { accepted = true, queuePosition = 1 });
        }
        catch (BroadcastBusyException)
        {
            TryWriteJson(ctx, 409, new { error = "有播报正在进行，请稍候" });
        }
        catch (AnnouncementException e)
        {
            TryWriteJson(ctx, 400, new { error = e.Message });
        }
    }

    private void HandleStatus(HttpListenerContext ctx)
    {
        var (state, current, lastError) = quickCall.GetStatus();
        object payload = current == null
            ? new { state = state.ToString(), error = lastError == "" ? null : lastError }
            : new
            {
                state = state.ToString(),
                current = new
                {
                    caller = current.Caller,
                    students = current.Students,
                    destination = current.Destination,
                    startedAt = current.StartedAtUtc,
                },
                error = lastError == "" ? null : lastError,
            };
        TryWriteJson(ctx, 200, payload);
    }

    private void HandleHistory(HttpListenerContext ctx)
    {
        var records = quickCall.GetHistory();
        TryWriteJson(ctx, 200, new
        {
            records = records.Select(r => new
            {
                at = r.AtUtc,
                students = r.Students,
                destination = r.Destination,
                caller = r.Caller,
                result = r.Result,
            }),
        });
    }

    private void ServeSpa(HttpListenerContext ctx)
    {
        var file = Path.Combine(pluginFolder, "Assets", "wwwroot", "index.html");
        if (!File.Exists(file))
        {
            TryWriteJson(ctx, 404, new { error = "Not Found" });
            return;
        }

        ctx.Response.ContentType = "text/html; charset=utf-8";
        var bytes = File.ReadAllBytes(file);
        ctx.Response.ContentLength64 = bytes.Length;
        ctx.Response.OutputStream.Write(bytes);
        ctx.Response.OutputStream.Close();
    }

    // ---- 小工具 ----

    private string? GetCodeSegment(HttpListenerContext ctx)
    {
        var segments = (ctx.Request.Url?.AbsolutePath ?? "/").Trim('/').Split('/');
        return segments.Length > 0 ? segments[0] : null;
    }

    private async Task<T?> ReadJsonBodyAsync<T>(HttpListenerContext ctx) where T : class
    {
        try
        {
            // 64KB 上限：手机端这点 JSON 用不满，设上限纯粹是防呆。
            if (ctx.Request.ContentLength64 is < 0 or > 64 * 1024)
            {
                TryWriteJson(ctx, 400, new { error = "请求体不合法" });
                return null;
            }

            using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
            var json = await reader.ReadToEndAsync();
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (Exception)
        {
            TryWriteJson(ctx, 400, new { error = "请求体不是合法的 JSON" });
            return null;
        }
    }

    private bool AllowRequest(string ip)
    {
        var now = DateTime.UtcNow;
        var allowed = true;
        _rateLimits.AddOrUpdate(ip,
            _ => (now, 1),
            (_, entry) =>
            {
                // 窗口 1 秒、上限 2 次（R8）。到点重开窗，窗口内超了就拒。
                if ((now - entry.Start).TotalSeconds >= 1)
                {
                    return (now, 1);
                }

                entry.Count++;
                allowed = entry.Count <= 2;
                return entry;
            });
        return allowed;
    }

    private void TryWriteJson(HttpListenerContext ctx, int status, object payload)
    {
        try
        {
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes);
            ctx.Response.OutputStream.Close();
        }
        catch (Exception e)
        {
            // 手机端中途断开（扫一半锁屏）是日常，写不回去就算了。
            logger.LogDebug(e, "[RemoteBroadcast] 响应写入失败（客户端可能已断开）");
        }
    }

    private sealed record RegisterRequest(string? TeacherName);

    private sealed record QuickCallRequest(List<string>? Students, string? Destination, string? Caller);
}

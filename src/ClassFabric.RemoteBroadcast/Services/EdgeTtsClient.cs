using System.Buffers.Binary;
using System.Globalization;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace ClassFabric.RemoteBroadcast.Services;

/// <summary>合成失败（断网、被微软拒绝、协议不认识等），消息可直接给手机端看。</summary>
public class SpeechSynthesisException(string message) : Exception(message);

/// <summary>
/// 自实现的最小 EdgeTTS 合成客户端：一次请求把整段文本合进内存流，不落盘。
/// 协议与 Sec-MS-GEC 鉴权算法来自公开的 edge-tts 项目（https://github.com/rany2/edge-tts，issue #290），
/// 本文件是独立实现、没有复制 vendors/EdgeTtsSharp 的代码——那个库没带许可证文件，保持距离更稳妥。
/// </summary>
public partial class EdgeTtsClient(ILogger<EdgeTtsClient> logger)
{
    // 微软 Edge「大声朗读」的固定 TrustedClientToken，全网公开多年的常量。
    private const string TrustedClientToken = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
    private const string ChromiumVersion = "143.0.3650.75";
    private const string OutputFormat = "audio-24khz-48kbitrate-mono-mp3";

    // 这组头是从 rany2/edge-tts 逐字对齐抄的清单——实测少一个都会被服务端 403 拒掉握手，
    // 尤其 Pragma/Cache-Control/Accept-Encoding 这三个看着无关紧要的缓存头，缺了直接进不去。
    // UA 里的 Chrome 版本必须是「主版本.0.0.0」格式，带完整 build 号反而会被认出来不是真 Edge。
    private static readonly string[] Headers =
    [
        "Pragma:no-cache",
        "Cache-Control:no-cache",
        "Origin:chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold",
        "Accept-Encoding:gzip, deflate, br, zstd",
        "Accept-Language:en-US,en;q=0.9",
        "User-Agent:Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/143.0.0.0 Safari/537.36 Edg/143.0.0.0",
    ];

    [GeneratedRegex(@"[<>&'""]")]
    private static partial Regex SsmlEscapeRegex();

    /// <summary>
    /// 合成整段文本，返回内存中的 MP3 流。失败一律抛 <see cref="SpeechSynthesisException"/>，
    /// 调用方拿到的消息可以原样发给手机端（不含堆栈）。
    /// </summary>
    public async Task<Stream> SynthesizeAsync(string text, string voice, CancellationToken cancellationToken)
    {
        // 合成是「老师点了呼叫」之后的第一段网络等待，20 秒还合不出来就当失败处理，
        // 别让学生干站着看 Overlay 等一个永远不来的声音。
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(20));

        var audio = new MemoryStream();
        try
        {
            using var ws = new ClientWebSocket();
            foreach (var header in Headers)
            {
                var idx = header.IndexOf(':');
                ws.Options.SetRequestHeader(header[..idx], header[(idx + 1)..]);
            }

            // Sec-MS-GEC：UTC ticks 按 3 秒取整后拼固定密钥再做 SHA256——微软的反滥用签名。
            // 注意机器时钟偏差太大（几分钟以上）会被 403，这是已知坑不是代码 bug。
            var connectionId = Guid.NewGuid().ToString("N");
            var gec = GenerateSecMsGecToken();
            var uri = new Uri(
                "wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1" +
                $"?TrustedClientToken={TrustedClientToken}" +
                $"&ConnectionId={connectionId}" +
                $"&Sec-MS-GEC={gec}" +
                $"&Sec-MS-GEC-Version=1-{ChromiumVersion}");
            logger.LogDebug("[RemoteBroadcast] EdgeTTS 连接：GEC 前缀 {Gec}，ConnectionId {Conn}", gec[..10], connectionId[..8]);
            await ws.ConnectAsync(uri, timeoutCts.Token);

            // 第一条消息声明输出格式，第二条才是正文 SSML，顺序反了服务端直接不理你。
            await ws.SendAsync(TextMemory(SpeechConfigMessage()), WebSocketMessageType.Text, true, timeoutCts.Token);
            await ws.SendAsync(TextMemory(SsmlMessage(connectionId, voice, text)), WebSocketMessageType.Text, true, timeoutCts.Token);

            // 接收循环：音频帧是「2 字节大端头长 + 头部 + 音频体」的三明治结构；
            // 文本帧里等到 Path:turn.end 才算合完。中途断开且没等到 turn.end 就按失败算，半截音频宁可不要。
            var buffer = new byte[32 * 1024];
            var turnEnd = false;
            while (ws.State == WebSocketState.Open && !turnEnd)
            {
                var result = await ws.ReceiveAsync(buffer, timeoutCts.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    // 服务端挂断时把状态码带出来，配合上面的文本帧日志才能看清它为什么不高兴。
                    logger.LogWarning("[RemoteBroadcast] EdgeTTS 连接被服务端关闭：{Status} {Description}",
                        ws.CloseStatus, ws.CloseStatusDescription);
                    break;
                }

                var span = buffer.AsSpan(0, result.Count);
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var frameText = Encoding.UTF8.GetString(span);
                    // 服务端拒绝合成时只在 text 帧里给原因然后挂断，留一条 Debug 日志备查。
                    if (logger.IsEnabled(LogLevel.Debug))
                    {
                        var pathIdx = frameText.IndexOf("Path:", StringComparison.Ordinal);
                        var path = pathIdx >= 0 ? frameText[(pathIdx + 5)..].Split('\r')[0].Trim() : "?";
                        logger.LogDebug("[RemoteBroadcast] EdgeTTS 收到文本帧 Path:{Path} 长度 {Length}", path, result.Count);
                    }
                    if (frameText.Contains("Path:turn.end"))
                    {
                        turnEnd = true;
                    }

                    continue;
                }

                // 二进制帧：先读头长，再跳过头部，剩下的才是音频。头长 +2 还超帧长就是协议出问题了，
                // 这种帧不能拿来当音频用（拼进 MP3 会出爆音），直接丢。
                if (span.Length < 2)
                {
                    continue;
                }

                var headerLength = BinaryPrimitives.ReadUInt16BigEndian(span);
                if (span.Length < headerLength + 2)
                {
                    logger.LogWarning("[RemoteBroadcast] 收到不完整的音频帧（帧长 {Length}，头长 {Header}），已丢弃", result.Count, headerLength);
                    continue;
                }

                audio.Write(span[(2 + headerLength)..]);
            }

            if (audio.Length == 0)
            {
                throw new SpeechSynthesisException("语音合成失败：未收到音频数据（请检查白板网络连接）");
            }

            audio.Position = 0;
            return audio;
        }
        catch (SpeechSynthesisException)
        {
            throw;
        }
        catch (Exception e) when (e is WebSocketException or HttpRequestException or IOException or OperationCanceledException)
        {
            throw new SpeechSynthesisException("语音合成失败：无法连接 EdgeTTS 服务（请检查白板网络连接）");
        }
    }

    private static string GenerateSecMsGecToken()
    {
        // 公式与 rany2 逐字对齐：unix 秒 + WIN_EPOCH，先按 300 秒取整，再换算 100ns ticks 拼 token 做 SHA256。
        // 注意：不能图省事直接对 FILETIME ticks 取 3e9 网格——那样会留下最多 1 秒的亚秒残量，
        // 哈希差一位就是 403（这个坑我实打实踩过，探针验证了半天才定位到）。
        var seconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 11644473600L;
        seconds -= seconds % 300;
        var payload = seconds * 10_000_000L + TrustedClientToken;
        return Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(payload)));
    }

    private static string SpeechConfigMessage()
    {
        // X-Timestamp 必须带（rany2 两条消息都有）——探针里这头哪怕填 "now" 都能过，唯独整个缺失会被拒。
        var timestamp = DateTimeOffset.UtcNow.ToString(
            "ddd MMM dd yyyy HH:mm:ss 'GMT+0000 (Coordinated Universal Time)'", CultureInfo.InvariantCulture);
        // 注意：最后这段 JSON 千万别改成插值字符串——插值串里 "}}" 是转义字面量，"}}}}" 只会输出两个右括号，
        // JSON 直接残缺，服务端报 Bad request。这个坑真实踩过（蹲了一晚上对字节才发现），用普通拼接最安全。
        return $"X-Timestamp:{timestamp}Z\r\nContent-Type:application/json; charset=utf-8\r\nPath:speech.config\r\n\r\n" +
               "{\"context\":{\"synthesis\":{\"audio\":{\"metadataoptions\":{\"sentenceBoundaryEnabled\":\"false\",\"wordBoundaryEnabled\":\"false\"}," +
               "\"outputFormat\":\"" + OutputFormat + "\"}}}}";
    }

    private static string SsmlMessage(string requestId, string voice, string text)
    {
        // SSML 里文本必须转义，否则学生名字里混个 & 就能把整条请求废掉。
        var escaped = SsmlEscapeRegex().Replace(text, m => m.Value switch
        {
            "&" => "&amp;",
            "<" => "&lt;",
            ">" => "&gt;",
            "'" => "&apos;",
            _ => "&quot;",
        });
        var timestamp = DateTimeOffset.UtcNow.ToString(
            "ddd MMM dd yyyy HH:mm:ss 'GMT+0000 (Coordinated Universal Time)'", CultureInfo.InvariantCulture);
        return $"X-RequestId:{requestId}\r\nContent-Type:application/ssml+xml\r\nX-Timestamp:{timestamp}Z\r\nPath:ssml\r\n\r\n" +
               $"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='zh-CN'><voice name='{voice}'>" +
               $"<prosody pitch='+0Hz' rate='+0%' volume='+0%'>{escaped}</prosody></voice></speak>";
    }

    private static ReadOnlyMemory<byte> TextMemory(string s)
    {
        return new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(s));
    }
}

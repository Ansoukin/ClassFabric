using System.Text.Json;
using ClassFabric.RemoteBroadcast.Models;
using Microsoft.Extensions.Logging;

namespace ClassFabric.RemoteBroadcast.Services;

/// <summary>
/// 插件配置存储：PluginConfigFolder/config.json，线程安全、原子写入（临时文件 + 替换）。
/// </summary>
public class ConfigStore(string pluginConfigFolder, ILogger<ConfigStore> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _path = Path.Combine(pluginConfigFolder, "config.json");
    private readonly object _lock = new();

    public RemoteBroadcastConfig Config { get; private set; } = new();

    public void Load()
    {
        lock (_lock)
        {
            if (!File.Exists(_path))
            {
                logger.LogInformation("[RemoteBroadcast] 配置文件不存在，使用默认配置：{Path}", _path);
                SaveCore();
                return;
            }

            try
            {
                var json = File.ReadAllText(_path);
                Config = JsonSerializer.Deserialize<RemoteBroadcastConfig>(json, JsonOptions) ?? new RemoteBroadcastConfig();
                logger.LogInformation("[RemoteBroadcast] 配置已加载：{Path}", _path);
            }
            catch (Exception e)
            {
                // 配置坏了就重置默认：这是启动阶段，宁可丢配置也不能把整个插件加载掀翻。
                // 真遇到损坏时日志里留着堆栈，想救数据还能手工翻 config.json。
                Config = new RemoteBroadcastConfig();
                logger.LogError(e, "[RemoteBroadcast] 配置文件损坏，已重置为默认配置：{Path}", _path);
            }
        }
    }

    /// <summary>加锁变更并落盘。</summary>
    public void Update(Action<RemoteBroadcastConfig> mutate)
    {
        lock (_lock)
        {
            mutate(Config);
            SaveCore();
        }
    }

    /// <summary>取深拷贝快照，供 HTTP/UI 线程无锁读取。</summary>
    public RemoteBroadcastConfig Snapshot()
    {
        lock (_lock)
        {
            // JSON 往返做深拷贝看着笨，但名单上限 500 人这点量根本无感，
            // 换来的是调用方随便拿去用、不怕和写线程打架。
            return JsonSerializer.Deserialize<RemoteBroadcastConfig>(
                JsonSerializer.Serialize(Config, JsonOptions), JsonOptions) ?? new RemoteBroadcastConfig();
        }
    }

    private void SaveCore()
    {
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (dir != null && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // 先写临时文件再替换：写一半断电/被占用，最坏也就是多个 .tmp，不会留下半截 JSON。
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Config, JsonOptions));
            File.Move(tmp, _path, overwrite: true);
        }
        catch (Exception e)
        {
            // 写失败只记日志不往上抛——配置落盘失败不该把呼叫链路一起带走。
            logger.LogError(e, "[RemoteBroadcast] 配置写入失败：{Path}", _path);
        }
    }
}

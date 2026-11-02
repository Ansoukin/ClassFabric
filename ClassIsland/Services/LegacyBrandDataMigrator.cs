using System;
using System.IO;
using ClassIsland.Helpers;

namespace ClassIsland.Services;

/// <summary>
/// 品牌过渡期数据迁移：旧版本以 ClassFabric 为名存放数据，2.1.1.4 起改用标准目录名（ClassIsland），
/// 本类在启动最早期把旧目录并入新目录。仅 2.1.1.4–2.1.1.6 携带本特性，2.1.1.7 起删除本类与 App 中的调用点。
/// </summary>
public static class LegacyBrandDataMigrator
{
    /// <summary>
    /// 旧版曾以 classfabric:// 注册 URI 协议；过渡期新旧两个协议同时有效，2.1.1.7 起不再兼容旧协议。
    /// </summary>
    public const string LegacyUriScheme = "classfabric";

    public static void Run()
    {
        // 测试逃生门：开发机/测试机上可能装着旧版并保留其数据，设此变量可跳过迁移直接启动。
        if (Environment.GetEnvironmentVariable("CLASSISLAND_SKIP_LEGACY_MIGRATION") == "1")
        {
            return;
        }
        try
        {
            MigrateRoot(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
            MigrateRoot(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            MigrateAutostartShortcut();
            EnsureDualUriScheme();
            RewriteLegacyAutomationIds();
        }
        catch
        {
            // 迁移失败不阻塞启动：旧目录原样保留，应用在标准目录按首次启动流程运行，数据不会丢失。
        }
    }

    /// <summary>
    /// 旧版动作/触发器以 classfabric. 为前缀注册，标准版为 classisland.，配置文件里存的是注册 ID。
    /// 目录迁移只搬文件，改不了文件内容里的 ID，升级后自动化列表会因找不到对应实现而整批失效，
    /// 这里对自动化配置做前缀重写。仅处理 JSON 文本中带引号的完整字面量，且前缀带点号，
    /// 不会误伤其他内容。
    /// </summary>
    private static void RewriteLegacyAutomationIds()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClassIsland", "Config", "Automations");
        if (!Directory.Exists(dir))
        {
            return;
        }
        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            try
            {
                var content = File.ReadAllText(file);
                if (!content.Contains("classfabric."))
                {
                    continue;
                }
                File.WriteAllText(file, content.Replace("classfabric.", "classisland."));
                File.WriteAllText(file + ".bak-legacy-id", content);
            }
            catch
            {
                // 单个文件失败不影响其他文件与启动，用户可按备份恢复。
            }
        }
    }

    private static void MigrateRoot(string baseDir)
    {
        var source = Path.Combine(baseDir, "ClassFabric");
        var target = Path.Combine(baseDir, "ClassIsland");
        if (!Directory.Exists(source))
        {
            return;
        }
        if (!Directory.Exists(target))
        {
            // 同卷目录重命名是原子操作，要么完整发生要么不发生，不存在搬一半的状态。
            Directory.Move(source, target);
            return;
        }
        // 目标已存在不能直接跳过：残留的旧目标目录会把新数据挡在门外，升级后用户看到的是陈旧数据。
        // 以 Data\Settings.json 的修改时间判断两边新旧：源更新，就把目标改名备份后整体迁入，两边数据都不丢；
        // 源不比目标新，说明目标才是活跃数据，维持现状避免被旧数据覆盖。
        if (GetDataStamp(source) <= GetDataStamp(target))
        {
            return;
        }
        Directory.Move(target, target + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.Move(source, target);
    }

    private static DateTime GetDataStamp(string root)
    {
        var settings = Path.Combine(root, "Data", "Settings.json");
        return File.Exists(settings) ? File.GetLastWriteTime(settings) : DateTime.MinValue;
    }

    private static void MigrateAutostartShortcut()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        var startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        var legacy = Path.Combine(startup, "ClassFabric.lnk");
        var current = Path.Combine(startup, "ClassIsland.lnk");
        // 自启状态按 Startup 目录下的快捷方式文件名探测，改名才能保住老用户的开机自启设置。
        if (File.Exists(legacy) && !File.Exists(current))
        {
            File.Move(legacy, current);
        }
    }

    /// <summary>
    /// 旧版注册的 classfabric:// 注册表键升级后依然指向原安装路径（exe 文件名与安装目录都没变），无需重写；
    /// 这里只需把新的 classisland:// 也注册上，两个协议就都能拉起应用。
    /// </summary>
    private static void EnsureDualUriScheme()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        var legacyRegistered = Microsoft.Win32.Registry.CurrentUser
            .OpenSubKey($"Software\\Classes\\{LegacyUriScheme}") != null;
        if (legacyRegistered && !UriProtocolRegisterHelper.IsRegistered())
        {
            UriProtocolRegisterHelper.Register();
        }
    }
}

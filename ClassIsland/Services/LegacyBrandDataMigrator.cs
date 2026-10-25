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
        }
        catch
        {
            // 迁移失败不阻塞启动：旧目录原样保留，应用在标准目录按首次启动流程运行，数据不会丢失。
        }
    }

    private static void MigrateRoot(string baseDir)
    {
        var source = Path.Combine(baseDir, "ClassFabric");
        var target = Path.Combine(baseDir, "ClassIsland");
        if (!Directory.Exists(source) || Directory.Exists(target))
        {
            return;
        }
        // 同卷目录重命名是原子操作，要么完整发生要么不发生，不存在搬一半的状态。
        Directory.Move(source, target);
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

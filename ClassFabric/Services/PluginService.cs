using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Abstractions.Services.Management;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Enums;
using ClassIsland.Core.Models.Plugin;
using ClassIsland.Core.Services.Registry;
using ClassIsland.Models.Plugins;
using ClassIsland.Services.Management;
using ClassIsland.Shared;
using ClassIsland.Shared.Protobuf.AuditEvent;
using ClassIsland.Shared.Protobuf.Enum;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ClassIsland.Services;

/// <summary>
/// 插件服务
/// </summary>
public class PluginService : IPluginService
{
    public static readonly string PluginsRootPath = Path.Combine(CommonDirectories.AppRootFolderPath, "Plugins");

    internal static readonly string LegacyPluginsRootPath =
        Path.Combine(CommonDirectories.LegacyAppRootFolderPath, "Plugins");

    public static readonly string PluginsIndexPath = Path.Combine(CommonDirectories.AppConfigPath, "PluginsIndex");

    public static readonly string PluginsPkgRootPath = Path.Combine(CommonDirectories.AppCacheFolderPath, "PluginPackages");


    public static readonly string PluginManifestFileName = "manifest.yml";

    public static readonly string PluginConfigsFolderPath = Path.Combine(CommonDirectories.AppConfigPath, "Plugins");

    public static List<PluginInfo> PluginLoadedStatus { get; internal set; } =new();

    internal static readonly Dictionary<string, PluginLoadContext> PluginLoadContexts = new();

    internal static List<PluginManifest> InstalledPlugins { get; } = [];
    
    internal static List<PluginManifest> UninstalledPlugins { get; } = [];

    /// <summary>
    /// 插件诊断信息字典：{插件ID -> (用户消息, 技术细节)}
    /// </summary>
    internal static readonly Dictionary<string, (string UserMessage, string TechnicalDetails)> PluginDiagnostics = new();
    /// <summary>
    /// 处理插件安装
    /// </summary>
    public static void ProcessPluginsInstall()
    {
        if (!Directory.Exists(PluginsPkgRootPath))
        {
            Directory.CreateDirectory(PluginsPkgRootPath);
        }
        if (!Directory.Exists(PluginsRootPath))
        {
            Directory.CreateDirectory(PluginsRootPath);
        }

        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .WithTypeConverter(new OSPlatformTypeConverter_Yaml())
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        foreach (var pkgPath in Directory.EnumerateFiles(PluginsPkgRootPath).Where(x => Path.GetExtension(x) == IPluginService.PluginPackageExtension))
        {
            try
            {
                bool isEnabledBefore = true;
                using var pkg = ZipFile.OpenRead(pkgPath);
                var mf = pkg.GetEntry(PluginManifestFileName);
                if (mf == null)
                    continue;
                var mfText = new StreamReader(mf.Open()).ReadToEnd();
                var manifest = deserializer.Deserialize<PluginManifest>(mfText);
                var targetPath = Path.Combine(PluginsRootPath, manifest.Id);
                Console.Write($"正在处理插件安装: {manifest.Name}({manifest.Id},{manifest.Version})...");
                if (Directory.Exists(targetPath))
                {
                    if(File.Exists(Path.Combine(targetPath,".disabled")))
                    {
                        isEnabledBefore = false;
                    }
                    Directory.Delete(targetPath, true);
                }
                Directory.CreateDirectory(targetPath);
                ZipFile.ExtractToDirectory(pkgPath, targetPath);
                if(!isEnabledBefore) File.WriteAllText(Path.Combine(targetPath, ".disabled"), "");
                InstalledPlugins.Add(manifest);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            File.Delete(pkgPath);
            Console.WriteLine("完成!");
        }
    }

    /// <summary>
    /// 初始化插件
    /// </summary>
    public static void InitializePlugins(HostBuilderContext context, IServiceCollection services)
    {
        if (!Directory.Exists(PluginsRootPath))
        {
            Directory.CreateDirectory(PluginsRootPath);
        }

        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .WithTypeConverter(new OSPlatformTypeConverter_Yaml())
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var pluginDirs = Directory.EnumerateDirectories(PluginsRootPath)
            .Concat(Directory.Exists(LegacyPluginsRootPath)
                ? Directory.EnumerateDirectories(LegacyPluginsRootPath)
                : [])
            .Append(App.ApplicationCommand.ExternalPluginPath);
        // 预处理插件信息
        foreach (var pluginDir in pluginDirs)
        {
            if (string.IsNullOrWhiteSpace(pluginDir))
                continue;
            var manifestPath = Path.Combine(pluginDir, PluginManifestFileName);
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            var manifestYaml = File.ReadAllText(manifestPath);
            var manifest = deserializer.Deserialize<PluginManifest?>(manifestYaml);
            if (manifest == null)
            {
                continue;
            }
            var info = new PluginInfo
            {
                Manifest = manifest,
                IsLocal = true,
                PluginFolderPath = Path.GetFullPath(pluginDir),
                RealIconPath = Path.Combine(Path.GetFullPath(pluginDir), manifest.Icon)
            };
            if (info.IsUninstalling)
            {
                Console.Write($"正在处理插件卸载: {manifest.Name}({manifest.Id},{manifest.Version})...");
                Directory.Delete(pluginDir, true);
                UninstalledPlugins.Add(manifest);
                Console.WriteLine("完成!");
                continue;
            }
            if (IPluginService.LoadedPluginsIds.Contains(manifest.Id))
                continue;
            IPluginService.LoadedPluginsIds.Add(manifest.Id);
            IPluginService.LoadedPluginsInternal.Add(info);
            if (!info.IsEnabled)
            {
                info.LoadStatus = PluginLoadStatus.Disabled;
                PluginLoadedStatus.Add(info);
            }
            if (info.IsEnabled && Version.TryParse(info.Manifest.ApiVersion, out var apiVersion) && apiVersion < new Version(2, 0, 0, 0))
            {
                info.LoadStatus = PluginLoadStatus.Error;
                info.Exception = new InvalidOperationException($"不兼容的 API 版本 {apiVersion}。插件的 API 版本需要至少为 2.0.0.0 才能被当前版本的 ClassFabric 加载。");
                PluginLoadedStatus.Add(info);
            }
        }
        var loadOrder = ResolveLoadOrder(IPluginService.LoadedPluginsInternal.Where(x => x.LoadStatus == PluginLoadStatus.NotLoaded).ToList());
        Console.WriteLine($"Resolved load order: {string.Join(", ", loadOrder)}");

        var forceMonoPluginLoadBehavior =
            Environment.GetEnvironmentVariable("ClassFabric_DebugForceMonoPluginLoadBehavior") == "1" ||
            Environment.GetEnvironmentVariable("ClassFabric_DebugSuppressMacOSPluginLoadBehavior") == "1";
        // 加载插件
        foreach (var id in loadOrder)
        {
            var info = IPluginService.LoadedPluginsInternal.First(x => x.Manifest.Id == id);
            var manifest = info.Manifest;
            var pluginDir = info.PluginFolderPath;
            try
            {
                var fullPath = Path.GetFullPath(Path.Combine(pluginDir, manifest.EntranceAssembly));
                // 检查插件兼容性
                var compatCheck = PluginCompatibilityDiagnosticService.CheckPluginCompatibility(fullPath);
                if (compatCheck.Status == PluginCompatibilityDiagnosticService.CompatibilityStatus.Incompatible)
                {
                    PluginDiagnostics[info.Manifest.Id] = (
                        compatCheck.UserFacingMessage ?? "插件不兼容",
                        GenerateTechnicalDiagnosticDetails(manifest, compatCheck.TechnicalDetails ?? ""));
                    info.LoadStatus = PluginLoadStatus.Error;
                    PluginLoadedStatus.Add(info);
                    continue;
                }

                var loadContext = new PluginLoadContext(info, fullPath, forceMonoPluginLoadBehavior);
                PluginLoadContexts[info.Manifest.Id] = loadContext;
                var asm = loadContext.LoadFromAssemblyName(
                    new AssemblyName(Path.GetFileNameWithoutExtension(fullPath)));
                var entrance = asm.ExportedTypes.FirstOrDefault(x =>
                    x.BaseType == typeof(PluginBase) ||
                    x.GetCustomAttributes().FirstOrDefault(a => a.GetType() == typeof(PluginEntrance)) != null);

                if (entrance == null)
                {
                    continue;
                }

                if (Activator.CreateInstance(entrance) is not PluginBase entranceObj)
                {
                    continue;
                }

                entranceObj.PluginConfigFolder = GetPluginConfigFolder(info);
                if (!Directory.Exists(entranceObj.PluginConfigFolder))
                    Directory.CreateDirectory(entranceObj.PluginConfigFolder);
                entranceObj.Info = info;
                // 在插件 Initialize 期间设置当前注册插件上下文，以便组件注册时自动捕获来源
                var previousPlugin = ComponentRegistryService.CurrentRegisteringPlugin.Value;
                ComponentRegistryService.CurrentRegisteringPlugin.Value = info;
                try
                {
                    entranceObj.Initialize(context, services);
                }
                finally
                {
                    ComponentRegistryService.CurrentRegisteringPlugin.Value = previousPlugin;
                }
                services.AddSingleton(typeof(PluginBase), entranceObj);
                services.AddSingleton(entrance, entranceObj);
                info.LoadStatus = PluginLoadStatus.Loaded;
                // Console.WriteLine($"Initialized plugin: {pluginDir} ({manifest.Version})");
                PluginLoadedStatus.Add(info);
            }
            catch (Exception ex)
            {
                info.Exception = ex;
                info.LoadStatus = PluginLoadStatus.Error;
                PluginDiagnostics[manifest.Id] = (
                    GenerateUserDiagnosticMessage(ex),
                    GenerateTechnicalDiagnosticDetails(ex, manifest));
                PluginLoadedStatus.Add(info);
                // Console.WriteLine($"Failed to initialize plugin {manifest.Name}:"+ex);
            }
        }
        
        AppBase.Current.AppStarted += CurrentOnAppStarted;
    }

    private static void CurrentOnAppStarted(object? sender, EventArgs e)
    {
        if (IAppHost.TryGetService<IManagementService>() is not
            { IsManagementEnabled: true, Connection: ManagementServerConnection connection })
        {
            return;
        }

        foreach (var i in InstalledPlugins)
        {
            connection.LogAuditEvent(AuditEvents.PluginInstalled, new PluginInstalled()
            {
                PluginId = i.Id,
                Version = i.Version
            });
        }
        foreach (var i in UninstalledPlugins)
        {
            connection.LogAuditEvent(AuditEvents.PluginUninstalled, new PluginUninstalled()
            {
                PluginId = i.Id,
                Version = i.Version
            });
        }
    }

    private static string GetPluginConfigFolder(PluginInfo info)
    {
        var isLegacyPlugin = info.PluginFolderPath.StartsWith(
            CommonDirectories.LegacyAppRootFolderPath,
            StringComparison.OrdinalIgnoreCase);
        var configRoot = isLegacyPlugin ? CommonDirectories.LegacyAppConfigPath : PluginConfigsFolderPath;
        return Path.Combine(configRoot, "Plugins", info.Manifest.Id);
    }

    /// <summary>
    /// 异步导出指定插件为插件包
    /// </summary>
    /// <param name="id">插件ID</param>
    /// <param name="outputPath">输出文件位置</param>
    /// <exception cref="ArgumentException">当找不到指定插件时抛出此异常</exception>
    public static async Task PackagePluginAsync(string id, string outputPath)
    {
        if (IPluginService.LoadedPlugins.All(x => x.Manifest.Id != id))
        {
            throw new ArgumentException($"找不到插件 {id}。", nameof(id));
        }
        await using var outputStream = File.Create(outputPath);
        await PackagePluginAsync(id, outputStream);
    }

    /// <summary>
    /// 异步导出指定插件到流
    /// </summary>
    public static async Task PackagePluginAsync(string id, Stream outputStream)
    {
        var plugin = IPluginService.LoadedPlugins.FirstOrDefault(x => x.Manifest.Id == id);
        if (plugin == null)
        {
            throw new ArgumentException($"找不到插件 {id}。", nameof(id));
        }

        await Task.Run(() =>
        {
            ZipFile.CreateFromDirectory(plugin.PluginFolderPath, outputStream);
        });
    }

    private static List<string> ResolveLoadOrder(List<PluginInfo> plugins)
    {
        var nodes = plugins
            .Where(x => x.LoadStatus == PluginLoadStatus.NotLoaded)
            .ToDictionary(
            x => x.Manifest.Id, 
            x => new DependencyNode(x));
        foreach (var i in nodes)
        {
            ResolveDependencyNode(nodes, i.Value, []);
        }
        return nodes
            .Where(x => x.Value.Plugin.LoadStatus == PluginLoadStatus.NotLoaded)
            .OrderBy(x => x.Value.DependencyTreeDepth)
            .Select(x => x.Key)
            .ToList();
    }

    private static void ResolveDependencyNode(Dictionary<string, DependencyNode> allNodes, DependencyNode node, List<DependencyNode> walkingNodes)
    {
        if (node.IsDiscovered)
        {
            return;
        }

        if (walkingNodes.Contains(node))
        {
            throw new InvalidOperationException(
                $"检测到循环依赖：{string.Join(" -> ", walkingNodes.Select(x => x.Plugin.Manifest.Id))}");
        }

        node.IsDiscovered = true;
        var depth = 0;
        foreach (var i in node.Plugin.Manifest.Dependencies)
        {
            if (!allNodes.TryGetValue(i.Id, out var dependency) || dependency.Plugin.LoadStatus != PluginLoadStatus.NotLoaded)
            {
                if (i.IsRequired)
                {
                    node.Plugin.LoadStatus = PluginLoadStatus.Error;
                    node.Plugin.Exception = new InvalidOperationException($"插件 {node.Plugin.Manifest.Id} 依赖的必选插件 {i.Id} 不存在或处于无法加载状态。");
                    return;
                }
                continue;
            }

            ResolveDependencyNode(allNodes, dependency, walkingNodes);
            depth = Math.Max(depth, dependency.DependencyTreeDepth);
        }
        node.DependencyTreeDepth = depth + 1;

    }

    /// <summary>
    /// 生成用户可读的诊断信息
    /// </summary>
    private static string GenerateUserDiagnosticMessage(Exception ex)
    {
        // Try to detect Avalonia version mismatch from exception
        if (ex.Message.Contains("Avalonia") || ex.Message.Contains("FluentAvalonia"))
        {
            return "该插件可能基于不兼容的 UI 框架版本构建。请确认插件与当前 ClassFabric 版本兼容，或联系插件作者更新。";
        }

        if (ex is MissingMethodException)
        {
            return "该插件缺少必要的方法引用，可能是版本不兼容导致的。请联系插件作者进行更新。";
        }

        if (ex is FileNotFoundException && ex.Message.Contains("dll"))
        {
            return "该插件缺少依赖的程序集。请确认插件完整性，或联系插件作者。";
        }

        // Generic fallback message
        return "该插件加载失败。请确认插件版本与当前 ClassFabric 版本兼容，或联系插件作者。";
    }

    /// <summary>
    /// 生成技术诊断详情
    /// </summary>
    private static string GenerateTechnicalDiagnosticDetails(PluginManifest manifest, string compatibilityDetails)
    {
        return string.Join(Environment.NewLine,
        [
            "=== 插件清单 ===",
            $"插件 ID: {manifest.Id}",
            $"插件名称: {manifest.Name}",
            $"插件版本: {manifest.Version}",
            $"API 版本: {manifest.ApiVersion}",
            $"入口程序集: {manifest.EntranceAssembly}",
            "",
            compatibilityDetails
        ]);
    }

    /// <summary>
    /// 生成技术诊断详情
    /// </summary>
    private static string GenerateTechnicalDiagnosticDetails(Exception ex, PluginManifest manifest)
    {
        var details = new System.Text.StringBuilder();

        details.AppendLine("=== 插件加载失败诊断信息 ===");
        details.AppendLine($"插件 ID: {manifest.Id}");
        details.AppendLine($"插件名称: {manifest.Name}");
        details.AppendLine($"插件版本: {manifest.Version}");
        details.AppendLine($"API 版本: {manifest.ApiVersion}");
        details.AppendLine($"入口程序集: {manifest.EntranceAssembly}");
        details.AppendLine();
        details.AppendLine("=== 运行时环境 ===");
        details.AppendLine($"ClassFabric 版本: {typeof(PluginService).Assembly.GetName().Version}");
        details.AppendLine($"Avalonia 版本: {GetAssemblyVersion("Avalonia.Base")}");
        details.AppendLine($"FluentAvalonia 版本: {GetAssemblyVersion("FluentAvalonia")}");
        details.AppendLine();
        details.AppendLine("=== 异常信息 ===");
        details.AppendLine($"异常类型: {ex.GetType().FullName}");
        details.AppendLine($"异常消息: {ex.Message}");
        if (!string.IsNullOrEmpty(ex.StackTrace))
        {
            details.AppendLine();
            details.AppendLine("=== 堆栈跟踪 ===");
            details.AppendLine(ex.StackTrace);
        }

        if (ex.InnerException != null)
        {
            details.AppendLine();
            details.AppendLine("=== 内部异常 ===");
            details.AppendLine($"类型: {ex.InnerException.GetType().FullName}");
            details.AppendLine($"消息: {ex.InnerException.Message}");
        }

        return details.ToString();
    }

    /// <summary>
    /// 获取程序集版本
    /// </summary>
    private static string GetAssemblyVersion(string assemblyName)
    {
        try
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == assemblyName);
            return asm?.GetName().Version?.ToString() ?? "未加载";
        }
        catch
        {
            return "未知";
        }
    }

    internal (string UserMessage, string TechnicalDetails)? GetPluginDiagnosticInfo(string pluginId)
    {
        if (PluginDiagnostics.TryGetValue(pluginId, out var diagnostic))
        {
            return diagnostic;
        }
        return null;
    }
}

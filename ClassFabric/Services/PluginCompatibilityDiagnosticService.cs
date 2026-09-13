using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace ClassIsland.Services;

/// <summary>
/// 插件兼容性诊断服务
/// </summary>
internal sealed class PluginCompatibilityDiagnosticService
{
    internal enum CompatibilityStatus
    {
        Compatible,
        Incompatible,
        Unknown
    }

    internal sealed record CompatibilityCheckResult(
        CompatibilityStatus Status,
        string? UserFacingMessage,
        string? TechnicalDetails);

    /// <summary>
    /// 检查插件是否与当前主机兼容。
    /// </summary>
    public static CompatibilityCheckResult CheckPluginCompatibility(string pluginAssemblyPath)
    {
        var hostAvaloniaVersion = GetLoadedAssemblyVersion("Avalonia.Base");
        var hostFluentAvaloniaVersion = GetLoadedAssemblyVersion("FluentAvalonia");
        if (hostAvaloniaVersion == null && hostFluentAvaloniaVersion == null)
        {
            return new(CompatibilityStatus.Unknown, null, null);
        }

        try
        {
            using var fileStream = File.OpenRead(pluginAssemblyPath);
            using var peReader = new PEReader(fileStream);
            if (!peReader.HasMetadata)
            {
                return new(CompatibilityStatus.Unknown, null, null);
            }

            var metadataReader = peReader.GetMetadataReader();
            var requestedAvaloniaVersion = GetAssemblyReferenceVersion(metadataReader, "Avalonia.Base");
            var requestedFluentAvaloniaVersion = GetAssemblyReferenceVersion(metadataReader, "FluentAvalonia");
            var reasons = new List<string>();

            if (hostAvaloniaVersion != null &&
                requestedAvaloniaVersion != null &&
                requestedAvaloniaVersion.Major < hostAvaloniaVersion.Major)
            {
                reasons.Add($"Avalonia.Base {requestedAvaloniaVersion} 低于当前宿主 {hostAvaloniaVersion}");
            }

            if (hostFluentAvaloniaVersion != null &&
                requestedFluentAvaloniaVersion != null &&
                requestedFluentAvaloniaVersion.Major < hostFluentAvaloniaVersion.Major)
            {
                reasons.Add($"FluentAvalonia {requestedFluentAvaloniaVersion} 低于当前宿主 {hostFluentAvaloniaVersion}");
            }

            if (reasons.Count == 0)
            {
                return new(CompatibilityStatus.Compatible, null, null);
            }

            var userMessage =
                "该插件基于不兼容的界面框架版本构建，与当前 ClassFabric 版本不兼容。请联系插件作者更新插件。";
            return new(
                CompatibilityStatus.Incompatible,
                userMessage,
                BuildTechnicalDetails(
                    pluginAssemblyPath,
                    requestedAvaloniaVersion,
                    requestedFluentAvaloniaVersion,
                    hostAvaloniaVersion,
                    hostFluentAvaloniaVersion,
                    reasons));
        }
        catch (Exception)
        {
            return new(CompatibilityStatus.Unknown, null, null);
        }
    }

    private static Version? GetAssemblyReferenceVersion(MetadataReader metadataReader, string assemblyName)
    {
        foreach (var handle in metadataReader.AssemblyReferences)
        {
            var assemblyReference = metadataReader.GetAssemblyReference(handle);
            if (string.Equals(metadataReader.GetString(assemblyReference.Name), assemblyName, StringComparison.OrdinalIgnoreCase))
            {
                return assemblyReference.Version;
            }
        }

        return null;
    }

    private static Version? GetLoadedAssemblyVersion(string assemblyName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(x => string.Equals(x.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase))
            ?.GetName().Version;
    }

    private static string BuildTechnicalDetails(
        string pluginAssemblyPath,
        Version? requestedAvaloniaVersion,
        Version? requestedFluentAvaloniaVersion,
        Version? hostAvaloniaVersion,
        Version? hostFluentAvaloniaVersion,
        IReadOnlyList<string> reasons)
    {
        return string.Join(Environment.NewLine,
        [
            "=== 插件兼容性诊断信息 ===",
            $"插件路径: {pluginAssemblyPath}",
            "",
            "=== 插件引用版本 ===",
            $"Avalonia.Base: {requestedAvaloniaVersion?.ToString() ?? "未引用"}",
            $"FluentAvalonia: {requestedFluentAvaloniaVersion?.ToString() ?? "未引用"}",
            "",
            "=== 当前宿主版本 ===",
            $"Avalonia.Base: {hostAvaloniaVersion?.ToString() ?? "未加载"}",
            $"FluentAvalonia: {hostFluentAvaloniaVersion?.ToString() ?? "未加载"}",
            "",
            "=== 判定原因 ===",
            .. reasons.Select(reason => $"- {reason}")
        ]);
    }
}

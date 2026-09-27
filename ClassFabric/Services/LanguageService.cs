using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using ClassIsland.Core;

namespace ClassIsland.Services;

internal static class LanguageService
{
    internal const string DefaultLanguageCode = "zh-Hans";

    internal static readonly IReadOnlyList<string> SupportedLanguages = ["zh-Hans", "en", "zh-Hant"];

    internal static CultureInfo ResolveCulture(string? languageCode)
    {
        // en 映射到具体区域而非中性文化，避免 CurrentUICulture 使用中性文化时的资源回退歧义。
        var cultureName = languageCode switch
        {
            "en" => "en-US",
            "zh-Hant" => "zh-Hant",
            _ => DefaultLanguageCode
        };
        try
        {
            return CultureInfo.GetCultureInfo(cultureName);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo(DefaultLanguageCode);
        }
    }

    internal static string ReadLanguageCodeFromSettingsFile()
    {
        try
        {
            var path = Path.Combine(CommonDirectories.AppRootFolderPath, "Settings.json");
            if (!File.Exists(path))
            {
                return DefaultLanguageCode;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("LanguageCode", out var property) ||
                property.ValueKind != JsonValueKind.String)
            {
                return DefaultLanguageCode;
            }

            return property.GetString() ?? DefaultLanguageCode;
        }
        catch (Exception)
        {
            // 启动极早期调用，日志系统未必就绪；任何异常一律回退默认语言。
            return DefaultLanguageCode;
        }
    }
}

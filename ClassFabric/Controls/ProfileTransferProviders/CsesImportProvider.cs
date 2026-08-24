using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using ClassFabric.Core.Abstractions.Controls;
using ClassFabric.Core.Abstractions.Controls.ProfileTransferProviders;
using ClassFabric.Core.Abstractions.Services;
using ClassFabric.Core.Helpers.UI;
using ClassFabric.Services;
using ClassFabric.Shared;
using ClassFabric.Shared.Extensions;
using ClassFabric.Shared.Helpers;
using ClassFabric.Shared.Models.Profile;
using CsesSharp;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.Logging;

namespace ClassFabric.Controls.ProfileTransferProviders;

public partial class CsesImportProvider : GenericImportProviderBase
{
    private IProfileService ProfileService { get; } = IAppHost.GetService<IProfileService>();
    
    public CsesImportProvider() : base()
    {
        ImportFileHeader = "CESE 文件路径";
        FileTypes =
        [
            new FilePickerFileType("CSES 课表文件")
            {
                Patterns = ["*.yml", "*.yaml"]
            }
        ];
    }

    public override async Task<bool> InvokeTransfer()
    {
        try
        {
            var csesProfile = CsesLoader.LoadFromYamlFile(SourceFilePath);
            var templateProfileJson =
                await new StreamReader(AssetLoader.Open(new Uri("avares://ClassFabric/Assets/default-subjects.json"))).ReadToEndAsync();
            var templateProfile = JsonSerializer.Deserialize<Profile>(templateProfileJson);
            var profile = csesProfile.ToClassFabricObject(ImportType == 0 ? ProfileService.Profile : templateProfile);
            if (ImportType == 1)
            {
                var path = System.IO.Path.Combine(Services.ProfileService.ProfilePath, NewProfileName + ".json");
                if (File.Exists(path))
                {
                    throw new InvalidOperationException($"无法导入课表：{path} 已存在。");
                }
                ConfigureFileHelper.SaveConfig(path, profile);
            }

            this.ShowSuccessToast("导入成功。");
            return true;
        }
        catch (Exception exception)
        {
            var logger = IAppHost.GetService<ILogger<CsesImportProvider>>();
            logger.LogError(exception, "导入 CSES 课表失败");
            this.ShowErrorToast($"无法导入 CSES 课表", exception);
            return false;
        }
    }
}
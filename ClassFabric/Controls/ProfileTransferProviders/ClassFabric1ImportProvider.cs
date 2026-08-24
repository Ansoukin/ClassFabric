using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using ClassFabric.Core.Abstractions.Controls.ProfileTransferProviders;
using ClassFabric.Core.Abstractions.Services;
using ClassFabric.Core.Helpers.UI;
using ClassFabric.Helpers.ProfileTransferHelpers;
using ClassFabric.Services;
using ClassFabric.Shared;
using ClassFabric.Shared.Helpers;
using Microsoft.Extensions.Logging;

namespace ClassFabric.Controls.ProfileTransferProviders;

public class ClassFabric1ImportProvider : GenericImportProviderBase
{
    private IProfileService ProfileService { get; } = IAppHost.GetService<IProfileService>();
    
    public ClassFabric1ImportProvider() : base()
    {
        ImportFileHeader = "ClassFabric 1.x 课表文件路径";
        FileTypes =
        [
            new FilePickerFileType("ClassFabric 1.x 课表文件")
            {
                Patterns = ["*.json"]
            }
        ];
        AllowMergeToCurrentProfile = false;
    }
    public override async Task<bool> InvokeTransfer()
    {
        try
        {
            var profile = ClassFabricV1ProfileTransferHelper.TransferClassFabricV1ProfileToClassFabricProfile(SourceFilePath);
            if (ImportType == 1)
            {
                var path = Path.Combine(Services.ProfileService.ProfilePath, NewProfileName + ".json");
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
            logger.LogError(exception, "导入 ClassFabric 1.x 课表失败");
            this.ShowErrorToast($"无法导入 ClassFabric 1.x 课表", exception);
            return false;
        }
    }
}
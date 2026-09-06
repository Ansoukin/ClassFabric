using System;
using System.IO;
using System.Linq;
using ClassIsland.Services;
using ClassIsland.Shared.Helpers;
using ClassIsland.Shared.Models.Profile;

namespace ClassIsland.Helpers.ProfileTransferHelpers;

public static class ClassIslandV1ProfileTransferHelper
{
    [Obsolete]
    public static Profile TransferClassIslandV1ProfileToClassFabricProfile(string path)
    {
        var config = ConfigureFileHelper.LoadConfigUnWrapped<Profile>(path, false);
        return TransferClassIslandV1ProfileToClassFabricProfile(config);
    }

    [Obsolete]
    public static Profile TransferClassIslandV1ProfileToClassFabricProfile(Stream stream)
    {
        var config = ConfigureFileHelper.LoadConfigUnWrapped<Profile>(stream);
        return TransferClassIslandV1ProfileToClassFabricProfile(config);
    }

    [Obsolete]
    private static Profile TransferClassIslandV1ProfileToClassFabricProfile(Profile config)
    {
        foreach (var tl in config.TimeLayouts)
        {
            foreach (var layoutItem in tl.Value.Layouts.Where(x =>
                         !string.IsNullOrWhiteSpace(x.StartSecond) && !string.IsNullOrWhiteSpace(x.EndSecond)))
            {
                layoutItem.StartTime = DateTime.TryParse(layoutItem.StartSecond, out var r1)
                    ? r1.TimeOfDay
                    : TimeSpan.Zero;
                layoutItem.EndTime = DateTime.TryParse(layoutItem.EndSecond, out var r2)
                    ? r2.TimeOfDay
                    : TimeSpan.Zero;
            }
        }

        return config;
    }
}

using System;
using System.Linq;
using ClassFabric.Services;
using ClassFabric.Shared.Helpers;
using ClassFabric.Shared.Models.Profile;

namespace ClassFabric.Helpers.ProfileTransferHelpers;

public static class ClassFabricV1ProfileTransferHelper
{
    [Obsolete]
    public static Profile TransferClassFabricV1ProfileToClassFabricProfile(string path)
    {
        var config = ConfigureFileHelper.LoadConfigUnWrapped<Profile>(path, false);
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
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
namespace ClassIsland.Models.Actions;

/// <summary>
/// "运行"行动设置。
/// </summary>
public partial class RunActionSettings : ObservableRecipient
{
    [ObservableProperty]
    RunActionRunType _runType;

    [ObservableProperty]
    string _value = "";

    [ObservableProperty]
    string _args = "";

    /// <summary>
    /// 以管理员身份运行命令。仅对命令类型生效；由运行时权限检测置位后持久化。
    /// </summary>
    [ObservableProperty]
    bool _isRunAsAdmin;

    /// <summary>
    /// "运行"行动运行类型。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RunActionRunType
    {
        /// <summary>
        /// 应用程序
        /// </summary>
        Application,

        /// <summary>
        /// 命令
        /// </summary>
        Command,

        /// <summary>
        /// 文件
        /// </summary>
        File,

        /// <summary>
        /// 文件夹
        /// </summary>
        Folder,

        /// <summary>
        /// Url 链接
        /// </summary>
        Url
    }
}

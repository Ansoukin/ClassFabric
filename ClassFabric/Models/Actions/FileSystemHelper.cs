using System.IO;

namespace ClassFabric.Models.Actions;

/// <summary>
/// 行动共用的文件系统操作辅助。
/// </summary>
public static class FileSystemHelper
{
    /// <summary>
    /// 递归复制整个目录树到目标位置。目标目录不存在时会自动创建；
    /// 同名文件的覆盖行为与 <see cref="File.Copy(string, string, bool)"/> 一致。
    /// </summary>
    public static void CopyDirectory(string sourceDirName, string destDirName, bool overwrite)
    {
        Directory.CreateDirectory(destDirName);

        foreach (var fileName in Directory.GetFiles(sourceDirName))
        {
            File.Copy(fileName, Path.Combine(destDirName, Path.GetFileName(fileName)), overwrite);
        }

        foreach (var dirName in Directory.GetDirectories(sourceDirName))
        {
            CopyDirectory(dirName, Path.Combine(destDirName, Path.GetFileName(dirName)), overwrite);
        }
    }
}

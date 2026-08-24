using System.Collections.ObjectModel;
using ClassFabric.Core.Models.Logging;
using DynamicData;

namespace ClassFabric.Core.Abstractions.Services.Logging;

/// <summary>
/// 应用日志服务
/// </summary>
public interface IAppLogService
{
    /// <summary>
    /// 已记录的日志
    /// </summary>
    SourceList<LogEntry> Logs { get; }
    internal void AddLog(LogEntry log);
}
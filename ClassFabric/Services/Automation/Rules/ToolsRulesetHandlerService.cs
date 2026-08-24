using System;
using System.Diagnostics;
using System.Globalization;
using ClassFabric.Core.Abstractions.Services;
using ClassFabric.Models.Rules;
using ClassFabric.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClassFabric.Services.Automation.Rules;

/// <summary>
/// 提供工具类条件判定规则的处理器。
/// </summary>
public class ToolsRulesetHandlerService
{
    public ILogger<ToolsRulesetHandlerService> Logger { get; }
    public IRulesetService RulesetService { get; }

    private ILessonsService? LessonsService { get; }

    // 进程枚举开销较大，短时间内的重复判定直接复用上一次结果。
    private static (string Name, bool Result, DateTime At) _processRunningCache;

    private static readonly TimeSpan ProcessCacheDuration = TimeSpan.FromSeconds(2);

    public ToolsRulesetHandlerService(ILogger<ToolsRulesetHandlerService> logger, IRulesetService rulesetService,
        ILessonsService? lessonsService = null)
    {
        Logger = logger;
        RulesetService = rulesetService;
        LessonsService = lessonsService;

        RulesetService.RegisterRuleHandler("classfabric.tools.processRunning", ProcessRunningHandler);
        RulesetService.RegisterRuleHandler("classfabric.tools.usingClassPlan", UsingClassPlanHandler);
        RulesetService.RegisterRuleHandler("classfabric.tools.usingTimeLayout", UsingTimeLayoutHandler);
        RulesetService.RegisterRuleHandler("classfabric.tools.inTimePeriod", InTimePeriodHandler);
    }

    private bool ProcessRunningHandler(object? settings)
    {
        if (settings is not ProcessRunningRuleSettings s) return false;
        var name = s.ProcessName;
        if (string.IsNullOrWhiteSpace(name)) return false;

        var cached = _processRunningCache;
        if (cached.Name == name && DateTime.Now - cached.At < ProcessCacheDuration)
        {
            return cached.Result;
        }

        bool result;
        try
        {
            result = Process.GetProcessesByName(name).Length > 0;
        }
        catch (Exception e)
        {
            Logger.LogWarning(e, "无法查询进程 {} 是否正在运行", name);
            return false;
        }

        _processRunningCache = (name, result, DateTime.Now);
        return result;
    }

    private bool UsingClassPlanHandler(object? settings)
    {
        if (settings is not UsingClassPlanRuleSettings s) return false;
        var lessons = ResolveLessonsService();
        if (lessons?.CurrentClassPlan == null) return false;
        try
        {
            // 课表对象本身不携带 ID，只能按日期重新解析出课表 Guid 再比较。
            lessons.GetClassPlanByDate(DateTime.Now, out var id);
            return id == s.ClassPlanId;
        }
        catch (Exception e)
        {
            Logger.LogWarning(e, "无法获取当前课表信息");
            return false;
        }
    }

    private bool UsingTimeLayoutHandler(object? settings)
    {
        if (settings is not UsingTimeLayoutRuleSettings s) return false;
        var current = ResolveLessonsService()?.CurrentClassPlan;
        return current != null && current.TimeLayoutId == s.TimeLayoutId;
    }

    private bool InTimePeriodHandler(object? settings)
    {
        if (settings is not InTimePeriodRuleSettings s) return false;
        if (!TryParseTime(s.Start, out var start) || !TryParseTime(s.End, out var end)) return false;

        var now = DateTime.Now.TimeOfDay;
        // 结束时间早于起始时间时视为跨零点区间，落在任一侧即算命中。
        return start <= end ? start <= now && now <= end : now >= start || now <= end;
    }

    private ILessonsService? ResolveLessonsService()
    {
        return LessonsService ?? IAppHost.Host?.Services.GetService<ILessonsService>();
    }

    private static bool TryParseTime(string text, out TimeSpan value)
    {
        return TimeSpan.TryParseExact(text, @"hh\:mm", CultureInfo.InvariantCulture, out value);
    }
}

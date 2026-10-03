using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ClassIsland.Core.Extensions;
using ClassIsland.Models.External.ClassWidgets;
using ClassIsland.Services;
using ClassIsland.Shared.Helpers;
using ClassIsland.Shared.Models.Profile;
using ClassFabric.Assets.Localization.Services.ProfileTransfer;

namespace ClassIsland.Helpers.ProfileTransferHelpers;

internal static class ClassWidgets2ProfileTransferHelper
{
    private const int SupportedSchemaVersion = 1;
    internal const int MaxSupportedWeekCycle = 9;

    internal static Cw2ImportAnalysis Analyze(Stream stream)
    {
        var source = ConfigureFileHelper.LoadConfigUnWrapped<Cw2Profile>(stream)
                     ?? throw new InvalidDataException(Localization.EmptyScheduleFile);
        return Analyze(source);
    }

    internal static Profile Convert(Cw2ImportAnalysis analysis, Profile? profile = null)
    {
        profile ??= ProfileService.CreateProfile(true);

        var subjectsByName = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var (id, subject) in profile.Subjects)
        {
            subjectsByName.TryAdd(subject.Name, id);
        }

        var sourceSubjectIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var sourceSubject in analysis.Subjects)
        {
            if (!subjectsByName.TryGetValue(sourceSubject.Name, out var id))
            {
                id = Guid.NewGuid();
                subjectsByName[sourceSubject.Name] = id;
                profile.Subjects[id] = new Subject { Name = sourceSubject.Name };
            }

            var subject = profile.Subjects[id];
            if (!string.IsNullOrWhiteSpace(sourceSubject.SimplifiedName))
            {
                subject.Initial = sourceSubject.SimplifiedName;
            }
            if (!string.IsNullOrWhiteSpace(sourceSubject.Teacher))
            {
                subject.TeacherName = sourceSubject.Teacher;
            }
            subject.IsOutDoor = !sourceSubject.IsLocalClassroom;
            sourceSubjectIds[sourceSubject.Id] = id;
        }

        foreach (var schedule in analysis.Schedules.OrderBy(x => x.Date == null ? 1 : 0))
        {
            var layoutItems = schedule.Entries.Select(entry => new TimeLayoutItem
            {
                TimeType = entry.Type == Cw2EntryType.Class ? 0 : 1,
                StartTime = entry.StartTime,
                EndTime = entry.EndTime,
                BreakName = entry.Type == Cw2EntryType.Class ? "" : entry.Title ?? ""
            }).ToList();

            var layoutId = profile.TimeLayouts.FirstOrDefault(x => LayoutEquals(x.Value, layoutItems)).Key;
            if (layoutId == Guid.Empty)
            {
                layoutId = Guid.NewGuid();
                var layout = new TimeLayout { Name = schedule.Name };
                foreach (var item in layoutItems)
                {
                    layout.Layouts.Add(item);
                }
                profile.TimeLayouts[layoutId] = layout;
            }

            var classPlan = new ClassPlan
            {
                Name = schedule.Name,
                TimeLayouts = profile.TimeLayouts,
                TimeLayoutId = layoutId
            };
            classPlan.Classes.Clear();
            foreach (var entry in schedule.Entries.Where(x => x.Type == Cw2EntryType.Class))
            {
                var subjectId = Guid.Empty;
                if (!string.IsNullOrWhiteSpace(entry.Title))
                {
                    subjectId = GetOrCreateSubject(entry.Title);
                }
                else if (!string.IsNullOrWhiteSpace(entry.SubjectId))
                {
                    sourceSubjectIds.TryGetValue(entry.SubjectId, out subjectId);
                }

                classPlan.Classes.Add(new ClassInfo { SubjectId = subjectId });
            }

            if (schedule.Date is { } date)
            {
                classPlan.TimeRule.Type = TimeRule.TimeRuleType.Date;
                classPlan.TimeRule.EnableDates.Clear();
                classPlan.TimeRule.EnableDates.Add(date);
            }
            else
            {
                classPlan.TimeRule.Type = TimeRule.TimeRuleType.Weekly;
                classPlan.TimeRule.WeekDay = schedule.DayOfWeek % 7;
                if (schedule.CyclePosition > 0)
                {
                    classPlan.TimeRule.WeekCountDivTotal = analysis.MaxWeekCycle;
                    classPlan.TimeRule.WeekCountDiv = schedule.CyclePosition;
                }
            }

            profile.ClassPlans[Guid.NewGuid()] = classPlan;
            classPlan.RefreshClassesList();
        }

        profile.RefreshTimeLayouts();
        return profile;

        Guid GetOrCreateSubject(string name)
        {
            if (subjectsByName.TryGetValue(name, out var id))
            {
                return id;
            }

            id = Guid.NewGuid();
            subjectsByName[name] = id;
            profile.Subjects[id] = new Subject { Name = name };
            return id;
        }
    }

    private static Cw2ImportAnalysis Analyze(Cw2Profile source)
    {
        if (source.Meta == null)
        {
            throw new InvalidDataException(Localization.MissingMetaInfo);
        }
        if (source.Meta.Version != SupportedSchemaVersion)
        {
            throw new InvalidDataException(string.Format(Localization.UnsupportedVersionFmt, source.Meta.Version, SupportedSchemaVersion));
        }
        if (source.Meta.MaxWeekCycle <= 0)
        {
            throw new InvalidDataException(Localization.MaxWeekCycleInvalid);
        }
        if (!DateOnly.TryParseExact(source.Meta.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var startDate))
        {
            throw new InvalidDataException(Localization.InvalidStartDate);
        }

        var warnings = new WarningCollector();
        var oversizedCycle = source.Meta.MaxWeekCycle > MaxSupportedWeekCycle;
        if (oversizedCycle)
        {
            warnings.Add("oversized-cycle",
                string.Format(Localization.WeekCycleTruncationWarningFmt, source.Meta.MaxWeekCycle, MaxSupportedWeekCycle));
        }

        var subjects = NormalizeSubjects(source.Subjects ?? [], warnings);
        var subjectIds = subjects.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        var timelines = NormalizeTimelines(source.Days ?? [], subjectIds, source.Meta.MaxWeekCycle,
            oversizedCycle, warnings, out var entryIds);
        var overrides = NormalizeOverrides(source.Overrides ?? [], subjectIds, entryIds,
            source.Meta.MaxWeekCycle, oversizedCycle, warnings);
        var schedules = ResolveSchedules(timelines, overrides, startDate, source.Meta.MaxWeekCycle,
            oversizedCycle, warnings);

        return new Cw2ImportAnalysis(startDate, source.Meta.MaxWeekCycle, oversizedCycle,
            subjects, schedules, warnings.Build());
    }

    private static List<Cw2NormalizedSubject> NormalizeSubjects(IEnumerable<Cw2Subject> source,
        WarningCollector warnings)
    {
        var results = new List<Cw2NormalizedSubject>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in source)
        {
            if (string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Name))
            {
                warnings.Add("invalid-subject", Localization.WarningInvalidSubject);
                continue;
            }
            if (!ids.Add(item.Id))
            {
                warnings.Add("duplicate-subject", Localization.WarningDuplicateSubject);
                continue;
            }

            var unsupportedFields = 0;
            if (!string.IsNullOrWhiteSpace(item.Icon)) unsupportedFields++;
            if (!string.IsNullOrWhiteSpace(item.Color)) unsupportedFields++;
            if (!string.IsNullOrWhiteSpace(item.Location)) unsupportedFields++;
            if (unsupportedFields > 0)
            {
                warnings.Add("unsupported-subject-fields", Localization.WarningUnsupportedSubjectFields, unsupportedFields);
            }

            results.Add(new Cw2NormalizedSubject(item.Id, item.Name, item.SimplifiedName, item.Teacher,
                item.LegacyIsLocalClassroom ?? item.IsLocalClassroom));
        }
        return results;
    }

    private static List<Cw2NormalizedTimeline> NormalizeTimelines(IEnumerable<Cw2Timeline> source,
        HashSet<string> subjectIds, int maxWeekCycle, bool oversizedCycle, WarningCollector warnings,
        out HashSet<string> entryIds)
    {
        var results = new List<Cw2NormalizedTimeline>();
        var timelineIds = new HashSet<string>(StringComparer.Ordinal);
        entryIds = new HashSet<string>(StringComparer.Ordinal);
        var seenEntryIds = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var item in source)
        {
            index++;
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                warnings.Add("invalid-timeline", Localization.WarningInvalidTimeline);
                continue;
            }
            if (!timelineIds.Add(item.Id))
            {
                warnings.Add("duplicate-timeline", Localization.WarningDuplicateTimeline);
                continue;
            }

            DateOnly? date = null;
            if (!string.IsNullOrWhiteSpace(item.Date))
            {
                if (DateOnly.TryParseExact(item.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var parsedDate))
                {
                    date = parsedDate;
                }
                else
                {
                    warnings.Add("invalid-date", Localization.WarningInvalidDate);
                }
            }

            var days = ParseDays(item.DayOfWeek, false, warnings, Localization.EntityTimeline);
            if (date == null && days == null)
            {
                warnings.Add("timeline-without-day", Localization.WarningTimelineWithoutDay);
                continue;
            }

            var weeks = ParseWeeks(item.Weeks, maxWeekCycle, false, warnings, Localization.EntityTimeline);
            if (weeks == null)
            {
                continue;
            }
            if (date == null && oversizedCycle && !weeks.IsAll)
            {
                warnings.Add("oversized-cycle-timeline", Localization.WarningOversizedCycleTimeline);
                continue;
            }

            var entries = new List<Cw2NormalizedEntry>();
            var entryIndex = 0;
            foreach (var entry in item.Entries ?? [])
            {
                entryIndex++;
                if (string.IsNullOrWhiteSpace(entry.Id))
                {
                    warnings.Add("invalid-entry", Localization.WarningInvalidEntry);
                    continue;
                }
                if (!seenEntryIds.Add(entry.Id))
                {
                    warnings.Add("duplicate-entry", Localization.WarningDuplicateEntry);
                    continue;
                }
                if (!TryParseEntryType(entry.Type, out var type))
                {
                    warnings.Add("unknown-entry-type", Localization.WarningUnknownEntryType);
                    continue;
                }
                if (!TryParseTime(entry.StartTime, out var start) || !TryParseTime(entry.EndTime, out var end) || end <= start)
                {
                    warnings.Add("invalid-entry-time", Localization.WarningInvalidEntryTime);
                    continue;
                }

                var subjectId = entry.SubjectId;
                if (!string.IsNullOrWhiteSpace(subjectId) && !subjectIds.Contains(subjectId))
                {
                    warnings.Add("unknown-entry-subject", Localization.WarningUnknownEntrySubject);
                    subjectId = null;
                }
                entryIds.Add(entry.Id);
                entries.Add(new Cw2NormalizedEntry(entry.Id, type, start, end, subjectId,
                    NullIfWhiteSpace(entry.Title), entryIndex));
            }

            results.Add(new Cw2NormalizedTimeline(item.Id, date, days ?? DayFilter.All, weeks, entries, index));
        }
        return results;
    }

    private static List<Cw2NormalizedOverride> NormalizeOverrides(IEnumerable<Cw2Override> source,
        HashSet<string> subjectIds, HashSet<string> entryIds, int maxWeekCycle, bool oversizedCycle,
        WarningCollector warnings)
    {
        var results = new List<Cw2NormalizedOverride>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var item in source)
        {
            index++;
            if (string.IsNullOrWhiteSpace(item.Id) || !ids.Add(item.Id))
            {
                warnings.Add("invalid-override-id", Localization.WarningInvalidOverrideId);
                continue;
            }
            if (string.IsNullOrWhiteSpace(item.EntryId) || !entryIds.Contains(item.EntryId))
            {
                warnings.Add("unknown-override-entry", Localization.WarningUnknownOverrideEntry);
                continue;
            }

            var days = ParseDays(item.DayOfWeek, true, warnings, Localization.EntityOverride);
            if (days == null)
            {
                continue;
            }
            var weeks = ParseWeeks(item.Weeks, maxWeekCycle, true, warnings, Localization.EntityOverride);
            if (weeks == null)
            {
                continue;
            }
            if (oversizedCycle && !weeks.IsAll)
            {
                warnings.Add("oversized-cycle-override", Localization.WarningOversizedCycleOverride);
                continue;
            }

            var subjectId = NullIfWhiteSpace(item.SubjectId);
            if (subjectId != null && !subjectIds.Contains(subjectId))
            {
                warnings.Add("unknown-override-subject", Localization.WarningUnknownOverrideSubject);
                subjectId = null;
            }

            TimeSpan? start = null;
            if (!string.IsNullOrWhiteSpace(item.StartTime))
            {
                if (TryParseTime(item.StartTime, out var parsed)) start = parsed;
                else warnings.Add("invalid-override-time", Localization.WarningInvalidOverrideTime);
            }
            TimeSpan? end = null;
            if (!string.IsNullOrWhiteSpace(item.EndTime))
            {
                if (TryParseTime(item.EndTime, out var parsed)) end = parsed;
                else warnings.Add("invalid-override-time", Localization.WarningInvalidOverrideTime);
            }

            var title = NullIfWhiteSpace(item.Title);
            if (subjectId == null && title == null && start == null && end == null)
            {
                continue;
            }
            results.Add(new Cw2NormalizedOverride(item.EntryId, days, weeks, subjectId, title, start, end, index));
        }
        return results;
    }

    private static List<Cw2ResolvedSchedule> ResolveSchedules(List<Cw2NormalizedTimeline> timelines,
        List<Cw2NormalizedOverride> overrides, DateOnly startDate, int maxWeekCycle, bool oversizedCycle,
        WarningCollector warnings)
    {
        var results = new List<Cw2ResolvedSchedule>();
        var dateTimelines = new HashSet<DateOnly>();
        foreach (var timeline in timelines.Where(x => x.Date != null).OrderBy(x => x.SourceIndex))
        {
            var date = timeline.Date!.Value;
            if (!dateTimelines.Add(date))
            {
                warnings.Add("duplicate-date-timeline", Localization.WarningDuplicateDateTimeline);
                continue;
            }
            var day = (int)date.DayOfWeek;
            day = day == 0 ? 7 : day;
            var cycle = GetCyclePosition(startDate, date, maxWeekCycle);
            var entries = ResolveEntries(timeline, overrides, day, cycle, warnings);
            if (entries.Count > 0)
            {
                results.Add(new Cw2ResolvedSchedule($"{date:yyyy-MM-dd}" + Localization.NamedDateSuffix, day, 0, date, entries));
            }
        }

        var cycleCount = oversizedCycle ? 1 : maxWeekCycle;
        for (var day = 1; day <= 7; day++)
        {
            var schedules = new List<Cw2ResolvedSchedule?>();
            for (var cycle = 1; cycle <= cycleCount; cycle++)
            {
                var timeline = timelines.FirstOrDefault(x => x.Date == null && x.Days.Matches(day)
                    && x.Weeks.Matches(cycle));
                if (timeline == null)
                {
                    schedules.Add(null);
                    continue;
                }

                var entries = ResolveEntries(timeline, overrides, day, cycle, warnings);
                schedules.Add(entries.Count == 0
                    ? null
                    : new Cw2ResolvedSchedule(GetDayName(day), day, cycle, null, entries));
            }

            var nonNull = schedules.Where(x => x != null).Cast<Cw2ResolvedSchedule>().ToList();
            if (nonNull.Count == 0)
            {
                continue;
            }
            if (nonNull.Count == cycleCount && nonNull.Skip(1).All(x => EntriesEqual(nonNull[0].Entries, x.Entries)))
            {
                results.Add(nonNull[0] with { CyclePosition = 0, Name = GetDayName(day) });
                continue;
            }

            foreach (var schedule in nonNull)
            {
                results.Add(schedule with { Name = GetCycleScheduleName(day, schedule.CyclePosition, maxWeekCycle) });
            }
        }
        return results;
    }

    private static List<Cw2ResolvedEntry> ResolveEntries(Cw2NormalizedTimeline timeline,
        List<Cw2NormalizedOverride> overrides, int day, int cycle, WarningCollector warnings)
    {
        var results = new List<Cw2ResolvedEntry>();
        foreach (var sourceEntry in timeline.Entries)
        {
            var start = sourceEntry.StartTime;
            var end = sourceEntry.EndTime;
            var subjectId = sourceEntry.SubjectId;
            var title = sourceEntry.Title;
            foreach (var item in overrides.Where(x => x.EntryId == sourceEntry.Id && x.Days.Matches(day)
                                                      && x.Weeks.Matches(cycle)).OrderBy(x => x.SourceIndex))
            {
                if (item.SubjectId != null) subjectId = item.SubjectId;
                if (item.Title != null) title = item.Title;
                if (item.StartTime != null) start = item.StartTime.Value;
                if (item.EndTime != null) end = item.EndTime.Value;
            }
            if (end <= start)
            {
                warnings.Add("invalid-resolved-time", Localization.WarningInvalidResolvedTime);
                continue;
            }
            results.Add(new Cw2ResolvedEntry(sourceEntry.Type, start, end, subjectId, title, sourceEntry.SourceIndex));
        }
        return results.OrderBy(x => x.StartTime).ThenBy(x => x.EndTime).ThenBy(x => x.SourceIndex).ToList();
    }

    private static DayFilter? ParseDays(JsonElement? value, bool emptyMeansAll, WarningCollector warnings,
        string owner)
    {
        if (value is not { } element || element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return emptyMeansAll ? DayFilter.All : null;
        }

        var values = new List<int>();
        var invalid = 0;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var single))
        {
            values.Add(single);
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                if (child.ValueKind == JsonValueKind.Number && child.TryGetInt32(out var day)) values.Add(day);
                else invalid++;
            }
        }
        else
        {
            invalid++;
        }

        invalid += values.RemoveAll(x => x is < 1 or > 7);
        if (invalid > 0)
        {
            warnings.Add("invalid-day", string.Format(Localization.WarningInvalidDayFmt, owner), invalid);
        }
        if (values.Count == 0)
        {
            return element.ValueKind == JsonValueKind.Array && element.GetArrayLength() == 0 && emptyMeansAll
                ? DayFilter.All
                : null;
        }
        return new DayFilter(false, values.ToHashSet());
    }

    private static WeekFilter? ParseWeeks(JsonElement? value, int maxWeekCycle, bool emptyMeansAll,
        WarningCollector warnings, string owner)
    {
        if (value is not { } element || element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return WeekFilter.All;
        }
        if (element.ValueKind == JsonValueKind.String)
        {
            if (string.Equals(element.GetString(), "all", StringComparison.OrdinalIgnoreCase))
            {
                return WeekFilter.All;
            }
            warnings.Add("invalid-weeks", string.Format(Localization.WarningUnknownWeeksFmt, owner));
            return null;
        }

        var values = new List<int>();
        var invalid = 0;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var single))
        {
            values.Add(single);
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                if (child.ValueKind == JsonValueKind.Number && child.TryGetInt32(out var week)) values.Add(week);
                else invalid++;
            }
        }
        else
        {
            invalid++;
        }

        invalid += values.RemoveAll(x => x < 1 || x > maxWeekCycle);
        if (invalid > 0)
        {
            warnings.Add("invalid-weeks", string.Format(Localization.WarningOutOfRangeWeeksFmt, owner), invalid);
        }
        if (values.Count == 0)
        {
            return element.ValueKind == JsonValueKind.Array && element.GetArrayLength() == 0 && emptyMeansAll
                ? WeekFilter.All
                : null;
        }
        return new WeekFilter(false, values.ToHashSet());
    }

    private static bool TryParseTime(string? value, out TimeSpan time) =>
        TimeSpan.TryParseExact(value, ["hh\\:mm", "h\\:mm"], CultureInfo.InvariantCulture, out time);

    private static bool TryParseEntryType(string? value, out Cw2EntryType type)
    {
        type = value switch
        {
            "class" => Cw2EntryType.Class,
            "break" => Cw2EntryType.Break,
            "activity" => Cw2EntryType.Activity,
            "free" => Cw2EntryType.Free,
            "preparation" => Cw2EntryType.Preparation,
            _ => Cw2EntryType.Unknown
        };
        return type != Cw2EntryType.Unknown;
    }

    private static int GetCyclePosition(DateOnly startDate, DateOnly date, int maxWeekCycle)
    {
        var elapsedWeeks = (int)Math.Floor((date.DayNumber - startDate.DayNumber) / 7.0);
        var position = elapsedWeeks % maxWeekCycle;
        if (position < 0) position += maxWeekCycle;
        return position + 1;
    }

    private static string GetDayName(int day) => day switch
    {
        1 => Localization.DayMonday,
        2 => Localization.DayTuesday,
        3 => Localization.DayWednesday,
        4 => Localization.DayThursday,
        5 => Localization.DayFriday,
        6 => Localization.DaySaturday,
        7 => Localization.DaySunday,
        _ => Localization.DayUnknown
    };

    private static string GetCycleScheduleName(int day, int cycle, int total) => total == 2
        ? $"{GetDayName(day)}{string.Format(Localization.OddEvenWeekSuffixFmt, cycle == 1 ? Localization.OddWeekLabel : Localization.EvenWeekLabel)}"
        : $"{GetDayName(day)}{string.Format(Localization.WeekCycleSuffixFmt, cycle, total)}";

    private static bool EntriesEqual(IReadOnlyList<Cw2ResolvedEntry> left, IReadOnlyList<Cw2ResolvedEntry> right)
    {
        if (left.Count != right.Count) return false;
        for (var i = 0; i < left.Count; i++)
        {
            if (left[i].Type != right[i].Type || left[i].StartTime != right[i].StartTime
                || left[i].EndTime != right[i].EndTime || left[i].SubjectId != right[i].SubjectId
                || left[i].Title != right[i].Title)
            {
                return false;
            }
        }
        return true;
    }

    private static bool LayoutEquals(TimeLayout layout, IReadOnlyList<TimeLayoutItem> items)
    {
        if (layout.Layouts.Count != items.Count) return false;
        for (var i = 0; i < items.Count; i++)
        {
            var left = layout.Layouts[i];
            var right = items[i];
            if (left.TimeType != right.TimeType || left.StartTime != right.StartTime || left.EndTime != right.EndTime
                || left.BreakName != right.BreakName)
            {
                return false;
            }
        }
        return true;
    }

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private sealed class WarningCollector
    {
        private readonly Dictionary<string, MutableWarning> _warnings = new(StringComparer.Ordinal);

        internal void Add(string key, string message, int count = 1)
        {
            if (_warnings.TryGetValue(key, out var warning))
            {
                warning.Count += count;
                return;
            }
            _warnings[key] = new MutableWarning(message, count);
        }

        internal IReadOnlyList<Cw2ImportWarning> Build() => _warnings.Values
            .Select(x => new Cw2ImportWarning(x.Message, x.Count))
            .ToList();

        private sealed class MutableWarning(string message, int count)
        {
            internal string Message { get; } = message;
            internal int Count { get; set; } = count;
        }
    }

    private sealed record DayFilter(bool IsAll, HashSet<int> Values)
    {
        internal static DayFilter All { get; } = new(true, []);
        internal bool Matches(int value) => IsAll || Values.Contains(value);
    }

    private sealed record WeekFilter(bool IsAll, HashSet<int> Values)
    {
        internal static WeekFilter All { get; } = new(true, []);
        internal bool Matches(int value) => IsAll || Values.Contains(value);
    }

    private sealed record Cw2NormalizedTimeline(string Id, DateOnly? Date, DayFilter Days, WeekFilter Weeks,
        List<Cw2NormalizedEntry> Entries, int SourceIndex);

    private sealed record Cw2NormalizedEntry(string Id, Cw2EntryType Type, TimeSpan StartTime, TimeSpan EndTime,
        string? SubjectId, string? Title, int SourceIndex);

    private sealed record Cw2NormalizedOverride(string EntryId, DayFilter Days, WeekFilter Weeks,
        string? SubjectId, string? Title, TimeSpan? StartTime, TimeSpan? EndTime, int SourceIndex);
}

internal sealed record Cw2ImportWarning(string Message, int Count);

internal sealed record Cw2ImportAnalysis(DateOnly StartDate, int MaxWeekCycle, bool HasOversizedCycle,
    IReadOnlyList<Cw2NormalizedSubject> Subjects, IReadOnlyList<Cw2ResolvedSchedule> Schedules,
    IReadOnlyList<Cw2ImportWarning> Warnings)
{
    internal int IgnoredItemCount => Warnings.Sum(x => x.Count);

    internal string WarningSummary
    {
        get
        {
            var builder = new StringBuilder();
            foreach (var warning in Warnings)
            {
                builder.Append("• ").Append(warning.Message);
                if (warning.Count > 1) builder.Append(string.Format(Localization.WarningCountSuffixFmt, warning.Count));
                builder.AppendLine();
            }
            return builder.ToString().TrimEnd();
        }
    }
}

internal sealed record Cw2NormalizedSubject(string Id, string Name, string? SimplifiedName, string? Teacher,
    bool IsLocalClassroom);

internal sealed record Cw2ResolvedSchedule(string Name, int DayOfWeek, int CyclePosition, DateOnly? Date,
    IReadOnlyList<Cw2ResolvedEntry> Entries);

internal sealed record Cw2ResolvedEntry(Cw2EntryType Type, TimeSpan StartTime, TimeSpan EndTime,
    string? SubjectId, string? Title, int SourceIndex);

internal enum Cw2EntryType
{
    Unknown,
    Class,
    Break,
    Activity,
    Free,
    Preparation
}

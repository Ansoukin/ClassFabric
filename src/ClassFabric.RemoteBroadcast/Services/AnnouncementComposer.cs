using System.Text.RegularExpressions;

namespace ClassFabric.RemoteBroadcast.Services;

/// <summary>播报构造失败（映射 HTTP 400 可读错误）。</summary>
public class AnnouncementException(string message) : Exception(message);

/// <summary>播报文本与视觉文本。</summary>
public sealed record AnnouncementResult(string VisualText, string SpeechText);

/// <summary>
/// 4.3 播报序列机制：单/多学生统一「段序列循环」模型——
/// 单学生 = 单句模板；多学生 = 引导句 + 名单段（「、」连接）；整体循环 N 次（1–5），
/// 语音一次性合成（句号天然分句停顿），Overlay 只显示一次完整循环文本（不堆叠）。
/// </summary>
public static partial class AnnouncementComposer
{
    private const int MaxSpeechLength = 1000;

    private static readonly string[] AllowedPlaceholders = ["Students", "Destination", "Caller"];

    [GeneratedRegex(@"\{([^}]*)\}")]
    private static partial Regex PlaceholderRegex();

    public static AnnouncementResult Compose(
        IReadOnlyList<string> studentNames, string destination, string caller,
        string templateSingle, string templateMultiGuide, int loopCount)
    {
        ValidateTemplate(templateSingle, nameof(templateSingle));
        ValidateTemplate(templateMultiGuide, nameof(templateMultiGuide));

        loopCount = Math.Clamp(loopCount, 1, 5);
        // 名单连接符用「、」不是随便选的：EdgeTTS 对顿号有自然停顿，正好拿来当名字间隔（任务书 4.3 定稿）。
        var roster = string.Join("、", studentNames);

        // 视觉与语音的分工：语音把同一段循环文本念 N 次，Overlay 只显示这段循环文本一次（不堆叠）。
        // 注意别在这里给视觉加「{Caller} 正在呼叫」头部——那句话归 Mask 阶段报（流程是：Mask 报「谁在呼叫」，
        // Overlay 只报「请谁到哪」）。岛容器只有 40px 高，两行叠一起必然溢出，这个坑真机验收时踩过。
        var singleCycle = studentNames.Count == 1
            ? Fill(templateSingle, roster, destination, caller)
            : Fill(templateMultiGuide, roster, destination, caller) + roster + "。";
        var visual = singleCycle;

        // 语音文本：段序列整体重复 N 次，段间以句号自然分隔
        var speech = string.Concat(Enumerable.Repeat(singleCycle, loopCount));

        if (speech.Length > MaxSpeechLength)
        {
            // 500 人名单 × 5 次循环的极端场景护栏——真到这一步让手机端提示减人，比让 EdgeTTS 合成一段十分钟的长文强。
            throw new AnnouncementException(
                $"播报文本超出 {MaxSpeechLength} 字符上限（当前 {speech.Length} 字符），请减少学生数或循环次数。");
        }

        return new AnnouncementResult(visual, speech);
    }

    /// <summary>校验模板：占位符仅限 {Students}/{Destination}/{Caller}，出现其他 {…} 视为非法。</summary>
    public static void ValidateTemplate(string template, string fieldName)
    {
        foreach (var m in PlaceholderRegex().Matches(template))
        {
            if (m is Match match && !AllowedPlaceholders.Contains(match.Groups[1].Value))
            {
                throw new AnnouncementException(
                    $"模板 {fieldName} 含非法占位符 {{{match.Groups[1].Value}}}（仅允许 {{Students}}/{{Destination}}/{{Caller}}）。");
            }
        }
    }

    private static string Fill(string template, string students, string destination, string caller)
    {
        return template
            .Replace("{Students}", students)
            .Replace("{Destination}", destination)
            .Replace("{Caller}", caller);
    }

    /// <summary>语音提示模板只含 {Caller} 一个占位符，走同一个校验器。</summary>
    public static string FillHint(string template, string caller)
    {
        ValidateTemplate(template, "语音提示模板");
        return template.Replace("{Caller}", caller);
    }
}

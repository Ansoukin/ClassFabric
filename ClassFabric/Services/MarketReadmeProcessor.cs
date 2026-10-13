using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Helpers;
using Markdig.Renderers.Normalize;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace ClassIsland.Services;

/// <summary>
/// 徽章图（下载量、License 一类 shields 徽标）：展示在详情页概览标题下方，不进正文轮播。
/// </summary>
internal sealed record MarketBadge(string ImageUrl, string? LinkUrl);

/// <summary>
/// 插件市场 README 预处理：把任意作者的 README 整理成详情页能受控呈现的内容。
/// </summary>
internal sealed record MarketReadmeResult(
    string OverviewTitle,
    IReadOnlyList<string> GalleryImages,
    IReadOnlyList<MarketBadge> Badges,
    string BodyMarkdown);

/// <summary>
/// README 清洗与图片抽取。
/// 结构化操作（标题降级、图片/徽章抽离、通知块剥标记、空链接清理）在 Markdig AST 上做，
/// 完成后规范化回 Markdown 文本交给渲染栈；只有语法刚性的归一（换行、HTML 注释、转义还原、
/// HTML 标题转写）留在文本层。渲染栈 Markdown.Avalonia 自带解析器只负责展示，不承担清洗语义。
/// </summary>
internal static partial class MarketReadmeProcessor
{
    private const int MaxGalleryImages = 6;

    // 市场上确有作者把图片语法整串转义成 \!\[...\](...)、通知块写成 \[!caution]（实测样本），
    // 这些转义在正文里本来就渲染成字面量，解析前先还原成正常语法
    [GeneratedRegex(@"\\([!\[\]()])")]
    private static partial Regex UnescapeRegex();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex HtmlCommentRegex();

    // HTML <h1 align="center"> 一类标题（实测样本写法）转成 # 标题，
    // 让标题抽取与降级统一走 AST，不再单写一套 HTML 标题识别
    [GeneratedRegex(@"<h([1-6])\b[^>]*>\s*(?<text>.*?)\s*</h\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex HtmlHeadingRegex();

    [GeneratedRegex(@"<img\b[^>]*\bsrc\s*=\s*(?:""(?<url>[^""]*)""|'(?<url>[^']*)')", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlImgRegex();

    // 抽走图后删标签用：必须吃掉整个 <img ...>，只删到 src 引号会残留 width/alt 属性尾巴
    [GeneratedRegex(@"<img\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlImgTagRegex();

    // HTML 块抽完图后，用它判定块里是否只剩标记（无正文文字）
    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex HtmlTagRegex();

    // 裸 URL 链接化；行内反引号包裹的不动，代码围栏由逐行扫描跳过。
    // Markdig 的 UseAutoLinks 在解析期已链接化绝大多数裸 URL，这里只兜 ASCII 边界外的情况
    // （比如全角冒号后紧跟的 URL），输入到这一步时已是规范化文本，误伤风险极小
    [GeneratedRegex(@"(?<!\]\()(?<!\()(?<!<)(?<!"")(?<!')(?<url>\bhttps?://[^\s<>)]+[^\s<>)}\.,;:!?)])")]
    private static partial Regex BareUrlRegex();

    [GeneratedRegex(@"^\s*\[[^\]]+\]:\s*\S+.*$", RegexOptions.Multiline)]
    private static partial Regex RefDefinitionLineRegex();

    [GeneratedRegex(@"^[ \t]+$", RegexOptions.Multiline)]
    private static partial Regex WhitespaceLineRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLinesRegex();

    /// <param name="baseUrl">README 来源（http URL 或本地目录），相对路径图片按它补全</param>
    /// <param name="pluginName">首个 H1 与插件名相同则删除，制式头部已经展示了</param>
    public static MarketReadmeResult Process(string markdown, string? baseUrl, string? pluginName)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return new MarketReadmeResult("", [], [], "");
        }

        var pre = markdown.Replace("\r\n", "\n").Replace("\r", "\n");
        pre = HtmlCommentRegex().Replace(pre, "");
        pre = UnescapeRegex().Replace(pre, "$1");
        pre = HtmlHeadingRegex().Replace(pre, m =>
            new string('#', int.Parse(m.Groups[1].Value)) + " " + m.Groups["text"].Value.Trim());

        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions() // 表格、任务列表、GitHub 通知块都由它解析
            .UseAutoLinks()          // 裸 URL 在解析期变链接，天然避开代码围栏与反引号
            .Build();
        // 主工程引用的 Markdown.Avalonia 在全局作用域占用了命名空间 Markdown，
        // 简单名 Markdown 会被解析成它而不是 Markdig 的引擎类，这里必须全限定
        var doc = Markdig.Markdown.Parse(pre, pipeline);

        var images = new List<string>();
        var badges = new List<MarketBadge>();
        string? overviewTitle = null;

        // 标题层级：首个 H1 匹配插件名则记为概览标题（正文里删），其余 H1 一律降为 H2，
        // 避免 README 原文的 H1 抢详情页制式头部的字号层级
        var firstH1 = doc.Descendants<HeadingBlock>().FirstOrDefault(h => h.Level == 1);
        if (firstH1 is not null)
        {
            var title = ExtractText(firstH1);
            if (!string.IsNullOrWhiteSpace(pluginName) &&
                string.Equals(title.Trim(), pluginName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                overviewTitle = title.Trim();
            }
            RemoveBlock(doc, firstH1);
        }
        foreach (var heading in doc.Descendants<HeadingBlock>().Where(h => h.Level == 1).ToList())
        {
            heading.Level = 2;
        }

        // Markdown 图片全部移出正文：徽章图与内容图分流；[![徽章](图)](外链) 的外链挂在父级链接上
        foreach (var image in doc.Descendants<LinkInline>().Where(l => l.IsImage).ToList())
        {
            var url = image.Url ?? image.Reference?.Url;
            if (!string.IsNullOrEmpty(url) && !url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var absolute = ResolveUrl(url, baseUrl);
                if (absolute is not null)
                {
                    string? linkUrl = null;
                    if (image.Parent is LinkInline { IsImage: false } outer)
                    {
                        var outerUrl = outer.Url ?? outer.Reference?.Url;
                        if (!string.IsNullOrEmpty(outerUrl))
                        {
                            linkUrl = ResolveUrl(outerUrl, baseUrl);
                        }
                    }
                    if (IsBadgeUrl(url))
                    {
                        badges.Add(new MarketBadge(absolute, linkUrl));
                    }
                    else
                    {
                        images.Add(absolute);
                    }
                }
            }
            image.Remove();
        }

        // HTML 图片只碰 Markdig 已识别为 HTML 的节点，不碰代码块里的示例标记
        foreach (var block in doc.Descendants<HtmlBlock>().ToList())
        {
            var content = block.Lines.ToString();
            if (!HtmlImgRegex().IsMatch(content))
            {
                continue;
            }
            foreach (Match match in HtmlImgRegex().Matches(content).Cast<Match>())
            {
                CollectImage(match.Groups["url"].Value, baseUrl, images, badges);
            }
            // 抽完图只剩标记（无正文文字）的块整块删；有正文的块保留，但把已抽走的 <img> 标签抠掉
            var withoutImg = HtmlImgTagRegex().Replace(content, "");
            if (HtmlTagRegex().Replace(withoutImg, "").Trim().Length == 0)
            {
                RemoveBlock(doc, block);
            }
            else
            {
                block.Lines = new StringLineGroup(withoutImg);
            }
        }
        foreach (var inline in doc.Descendants<HtmlInline>().ToList())
        {
            var match = HtmlImgRegex().Match(inline.Tag ?? "");
            if (match.Success)
            {
                CollectImage(match.Groups["url"].Value, baseUrl, images, badges);
                inline.Remove();
            }
        }

        // 图片摘除后 [![徽章](图)](外链) 会剩下 [](外链) 空壳；清一个可能让上层链接跟着变空，循环到稳定
        RemoveEmptyTextLinks(doc);

        // 图片独占的段落/列表项是残骸：空段落删、删空的列表项删、删空的列表删
        foreach (var paragraph in doc.Descendants<ParagraphBlock>().ToList())
        {
            if (!HasVisibleText(paragraph.Inline))
            {
                RemoveBlock(doc, paragraph);
            }
        }
        foreach (var item in doc.Descendants<ListItemBlock>().ToList())
        {
            if (item.Count == 0)
            {
                RemoveBlock(doc, item);
            }
        }
        foreach (var list in doc.Descendants<ListBlock>().ToList())
        {
            if (list.Count == 0)
            {
                RemoveBlock(doc, list);
            }
        }

        // 引用式定义不再被任何链接引用就删块，否则规范化会把 [label]: url 原样吐回正文
        var usedDefinitions = new HashSet<LinkReferenceDefinition>(
            doc.Descendants<LinkInline>().Select(l => l.Reference).Where(r => r is not null)!);
        foreach (var definition in doc.Descendants<LinkReferenceDefinition>().ToList())
        {
            if (!usedDefinitions.Contains(definition))
            {
                RemoveBlock(doc, definition);
            }
        }

        // AST 回写 Markdown：必须先 pipeline.Setup 把扩展渲染器（表格、自动链接等）注册进
        // NormalizeRenderer 再 Render，否则表格会被打碎成正文文字（Markdig 1.4 实测行为）
        var writer = new StringWriter();
        var renderer = new NormalizeRenderer(writer);
        pipeline.Setup(renderer);
        renderer.Render(doc);
        var body = LinkifyBareUrls(writer.ToString().Trim());
        body = WhitespaceLineRegex().Replace(body, "");
        body = BlankLinesRegex().Replace(body, "\n\n").Trim();

        return new MarketReadmeResult(overviewTitle ?? "", images.Take(MaxGalleryImages).ToList(), badges, body);
    }

    private static string ExtractText(HeadingBlock heading) =>
        string.Concat(heading.Inline?.Descendants<LiteralInline>().Select(l => l.Content.ToString()) ?? []);

    private static bool IsBadgeUrl(string url) =>
        url.Contains("shields.io", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("badge", StringComparison.OrdinalIgnoreCase);

    private static void CollectImage(string url, string? baseUrl, List<string> images, List<MarketBadge> badges)
    {
        if (string.IsNullOrWhiteSpace(url) || url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        var absolute = ResolveUrl(url, baseUrl);
        if (absolute is null)
        {
            return;
        }
        if (IsBadgeUrl(url))
        {
            badges.Add(new MarketBadge(absolute, null));
        }
        else
        {
            images.Add(absolute);
        }
    }

    // 相对路径按 baseUrl（http 地址或本地目录）补全；两者都补不了就丢弃
    private static string? ResolveUrl(string url, string? baseUrl)
    {
        url = url.Trim();
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            return new Uri(baseUri, url).ToString();
        }
        if (Path.IsPathRooted(baseUrl))
        {
            return Path.GetFullPath(Path.Combine(baseUrl, url.Replace('/', Path.DirectorySeparatorChar)));
        }
        return null;
    }

    // 解析出的块一定挂在树上，逐层向下找到所属容器摘除
    private static void RemoveBlock(ContainerBlock container, Block target)
    {
        var index = container.IndexOf(target);
        if (index >= 0)
        {
            container.RemoveAt(index);
            return;
        }
        foreach (var child in container.OfType<ContainerBlock>().ToList())
        {
            RemoveBlock(child, target);
        }
    }

    private static void RemoveEmptyTextLinks(MarkdownDocument doc)
    {
        bool changed;
        do
        {
            changed = false;
            foreach (var link in doc.Descendants<LinkInline>().Where(l => !l.IsImage).ToList())
            {
                if (!HasVisibleText(link))
                {
                    link.Remove();
                    changed = true;
                }
            }
        }
        while (changed);
    }

    // 可见文本只认字面量与行内代码里的非空白内容；换行、分隔符、结构容器都不算
    private static bool HasVisibleText(ContainerInline? container)
    {
        if (container is null)
        {
            return false;
        }
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    if (literal.Content.ToString().Any(c => !char.IsWhiteSpace(c)))
                    {
                        return true;
                    }
                    break;
                case CodeInline code:
                    if (code.Content.Any(c => !char.IsWhiteSpace(c)))
                    {
                        return true;
                    }
                    break;
                case ContainerInline nested:
                    if (HasVisibleText(nested))
                    {
                        return true;
                    }
                    break;
            }
        }
        return false;
    }

    private static string LinkifyBareUrls(string text)
    {
        var lines = text.Split('\n');
        var inFence = false;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.TrimStart().StartsWith("```"))
            {
                inFence = !inFence;
                continue;
            }
            if (inFence || line.Contains('`') || line.Contains("<http"))
                continue;
            if (RefDefinitionLineRegex().IsMatch(line))
                continue;

            lines[i] = BareUrlRegex().Replace(line, m => $"<{m.Groups["url"].Value}>");
        }
        return string.Join('\n', lines);
    }
}

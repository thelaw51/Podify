using System.Net;
using System.Text.RegularExpressions;

namespace Podify.Helpers;

public static class HtmlText
{
    private static readonly Regex ScriptStyle = new(
        @"<(script|style)\b[^>]*>[\s\S]*?</\1>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BlockBreak = new(
        @"</(p|div|h[1-6]|li|tr|blockquote)\s*>|<br\s*/?>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AnyTag = new(@"<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex MultiNewline = new(@"\n{3,}", RegexOptions.Compiled);
    private static readonly Regex InlineWhitespace = new(@"[ \t]+", RegexOptions.Compiled);

    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var s = ScriptStyle.Replace(html, string.Empty);
        s = BlockBreak.Replace(s, "\n\n");
        s = AnyTag.Replace(s, string.Empty);
        s = WebUtility.HtmlDecode(s);
        s = InlineWhitespace.Replace(s, " ");
        s = MultiNewline.Replace(s, "\n\n");
        return s.Trim();
    }
}

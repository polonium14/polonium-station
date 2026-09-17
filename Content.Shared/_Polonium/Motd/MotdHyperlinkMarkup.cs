using System.Diagnostics.CodeAnalysis;
using System.Text;
using Robust.Shared.Utility;

namespace Content.Shared._Polonium.Motd;

public static class MotdHyperlinkMarkup
{
    public const string TagName = "url";

    public static string ApplyForChat(string motd)
    {
        var formatted = Apply(motd);
        return FormattedMessage.ValidMarkup(formatted)
            ? formatted
            : FormattedMessage.EscapeText(motd);
    }

    public static string Apply(string motd)
    {
        if (string.IsNullOrEmpty(motd))
            return motd;

        var sb = new StringBuilder(motd.Length + 32);
        var i = 0;
        while (i < motd.Length)
        {
            if (motd[i] == '\\' && i + 1 < motd.Length)
            {
                sb.Append(motd[i]);
                sb.Append(motd[i + 1]);
                i += 2;
                continue;
            }

            if (TryReadMarkdownLink(motd, i, out var mdLen, out var label, out var mdHref)
                && IsSafeHttpUrl(mdHref))
            {
                sb.Append(MakeTag(label, mdHref));
                i += mdLen;
                continue;
            }

            sb.Append(motd[i]);
            i++;
        }

        return sb.ToString();
    }

    public static bool IsSafeHttpUrl(string url)
    {
        // client sandbox rejects System.UriKind
        if (!StartsWithHttpScheme(url, 0))
            return false;

        var start = url[4] is 's' or 'S' ? 8 : 7;
        if (start >= url.Length)
            return false;

        for (var i = 0; i < url.Length; i++)
        {
            var c = url[i];
            if (char.IsControl(c) || char.IsWhiteSpace(c) || c is '<' or '>' or '"')
                return false;
        }

        var host = url.AsSpan(start);
        var hostEnd = host.Length;
        for (var i = 0; i < host.Length; i++)
        {
            if (host[i] is '/' or '?' or '#')
            {
                hostEnd = i;
                break;
            }
        }

        host = host[..hostEnd];
        if (host.IsEmpty)
            return false;

        var at = host.LastIndexOf('@');
        if (at >= 0)
        {
            if (at + 1 >= host.Length)
                return false;

            host = host[(at + 1)..];
        }

        if (host.IsEmpty)
            return false;

        if (host[0] == '[')
        {
            var close = host.IndexOf(']');
            return close > 1;
        }

        var colon = host.IndexOf(':');
        if (colon == 0)
            return false;

        var name = colon < 0 ? host : host[..colon];
        return !name.IsEmpty;
    }

    private static bool TryReadMarkdownLink(
        string motd,
        int i,
        out int length,
        [NotNullWhen(true)] out string? label,
        [NotNullWhen(true)] out string? href)
    {
        length = 0;
        label = null;
        href = null;

        if (i >= motd.Length || motd[i] != '[')
            return false;

        var closeLabel = motd.IndexOf(']', i + 1);
        if (closeLabel < 0 || closeLabel + 1 >= motd.Length || motd[closeLabel + 1] != '(')
            return false;

        var urlStart = closeLabel + 2;
        if (!StartsWithHttpScheme(motd, urlStart))
            return false;

        var depth = 0;
        var j = urlStart;
        while (j < motd.Length)
        {
            var c = motd[j];
            if (char.IsWhiteSpace(c) || c is '[' or '<' or '"')
                return false;

            if (c == '(')
                depth++;
            else if (c == ')')
            {
                if (depth == 0)
                    break;
                depth--;
            }

            j++;
        }

        if (j >= motd.Length || motd[j] != ')')
            return false;

        var rawLabel = motd[(i + 1)..closeLabel];
        var rawHref = motd[urlStart..j];
        if (rawLabel.Length == 0 || rawHref.Length == 0)
            return false;

        label = rawLabel;
        href = rawHref;
        length = j - i + 1;
        return true;
    }

    private static bool StartsWithHttpScheme(string motd, int i)
    {
        var remaining = motd.Length - i;
        if (remaining >= 8 && motd.AsSpan(i, 8).Equals("https://", StringComparison.OrdinalIgnoreCase))
            return true;

        return remaining >= 7 && motd.AsSpan(i, 7).Equals("http://", StringComparison.OrdinalIgnoreCase);
    }

    private static string MakeTag(string text, string href)
    {
        return $"[{TagName}=\"{FormattedMessage.EscapeStringParameter(text)}\" href=\"{FormattedMessage.EscapeStringParameter(href)}\"/]";
    }
}

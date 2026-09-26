using AngleSharp.Html.Parser;

namespace Prismedia.Infrastructure.Reader;

/// <summary>
/// Strips HTML markup from provider responses (Wiktionary/Wikipedia return HTML-formatted prose)
/// down to plain, whitespace-normalized text suitable for the reader's lookup popover.
/// </summary>
internal static class HtmlTextStripper {
    private static readonly HtmlParser Parser = new();

    /// <summary>Returns the plain-text content of an HTML fragment, with collapsed whitespace.</summary>
    public static string StripToText(string? html) {
        if (string.IsNullOrWhiteSpace(html)) {
            return string.Empty;
        }

        var document = Parser.ParseDocument(html);
        var text = document.Body?.TextContent ?? document.TextContent ?? string.Empty;
        return NormalizeWhitespace(text);
    }

    private static string NormalizeWhitespace(string text) {
        var builder = new System.Text.StringBuilder(text.Length);
        var lastWasSpace = false;
        foreach (var ch in text) {
            if (char.IsWhiteSpace(ch)) {
                if (!lastWasSpace && builder.Length > 0) {
                    builder.Append(' ');
                }
                lastWasSpace = true;
            } else {
                builder.Append(ch);
                lastWasSpace = false;
            }
        }

        return builder.ToString().Trim();
    }
}

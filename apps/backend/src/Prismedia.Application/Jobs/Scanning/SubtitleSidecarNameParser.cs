using System.Globalization;
using Prismedia.Application.Files;
using Prismedia.Contracts.Media;

namespace Prismedia.Application.Jobs.Scanning;

/// <summary>
/// Language and descriptive label parsed from a subtitle sidecar filename.
/// </summary>
/// <param name="Language">Canonical culture code, or the undetermined-language code.</param>
/// <param name="Label">Human-readable descriptive tags that were not a language.</param>
public sealed record SubtitleSidecarName(string Language, string? Label);

/// <summary>
/// Pure filename parser for subtitle files named after their owning video stem.
/// </summary>
public static class SubtitleSidecarNameParser {
    private static readonly Lazy<IReadOnlyDictionary<string, CultureInfo>> CulturesByIsoCode =
        new(BuildCulturesByIsoCode);

    /// <summary>
    /// Parses an extension-free sidecar stem such as <c>Movie.pt-BR.forced</c> against the
    /// extension-free video stem <c>Movie</c>. Returns null when the filename is not owned by
    /// that video stem.
    /// </summary>
    public static SubtitleSidecarName? Parse(string videoStem, string sidecarStem) {
        if (string.IsNullOrEmpty(videoStem) ||
            string.IsNullOrEmpty(sidecarStem) ||
            !sidecarStem.StartsWith(videoStem, FileSystemPathComparison.Comparison)) {
            return null;
        }

        var suffix = sidecarStem[videoStem.Length..];
        if (suffix.Length > 0 && suffix[0] != '.') {
            return null;
        }

        return ParseTags(suffix.Length == 0 ? string.Empty : suffix[1..]);
    }

    private static SubtitleSidecarName ParseTags(string tags) {
        string? language = null;
        var labels = new List<string>();
        foreach (var segment in tags.Split('.', StringSplitOptions.RemoveEmptyEntries)) {
            if (language is null && TryResolveLanguage(segment, out var resolved)) {
                language = resolved;
                continue;
            }

            labels.Add(Humanize(segment));
        }

        return new SubtitleSidecarName(
            language ?? SubtitleLanguages.Undetermined,
            labels.Count == 0 ? null : string.Join(' ', labels));
    }

    private static bool TryResolveLanguage(string value, out string language) {
        var normalized = value.Replace('_', '-');
        if (string.Equals(normalized, SubtitleLanguages.Undetermined, StringComparison.OrdinalIgnoreCase)) {
            language = SubtitleLanguages.Undetermined;
            return true;
        }

        var subtags = normalized.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (subtags.Length == 0 ||
            !CulturesByIsoCode.Value.TryGetValue(subtags[0], out var culture) ||
            !HasSupportedLanguageTagShape(subtags)) {
            language = string.Empty;
            return false;
        }

        if (subtags.Length == 1) {
            language = culture.TwoLetterISOLanguageName;
            return true;
        }

        var canonical = string.Join('-', subtags.Skip(1).Prepend(culture.TwoLetterISOLanguageName));
        try {
            language = CultureInfo.GetCultureInfo(canonical).Name;
        } catch (CultureNotFoundException) {
            language = culture.TwoLetterISOLanguageName;
        }

        return true;
    }

    private static bool HasSupportedLanguageTagShape(IReadOnlyList<string> subtags) {
        var index = 1;
        if (index < subtags.Count && IsLetters(subtags[index], 4)) {
            index++;
        }

        if (index < subtags.Count &&
            (IsLetters(subtags[index], 2) || IsDigits(subtags[index], 3))) {
            index++;
        }

        return index == subtags.Count;
    }

    private static bool IsLetters(string value, int length) =>
        value.Length == length && value.All(char.IsAsciiLetter);

    private static bool IsDigits(string value, int length) =>
        value.Length == length && value.All(char.IsAsciiDigit);

    private static IReadOnlyDictionary<string, CultureInfo> BuildCulturesByIsoCode() {
        var result = new Dictionary<string, CultureInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.NeutralCultures)) {
            if (string.IsNullOrEmpty(culture.TwoLetterISOLanguageName)) {
                continue;
            }

            result.TryAdd(culture.TwoLetterISOLanguageName, culture);
            result.TryAdd(culture.ThreeLetterISOLanguageName, culture);
        }

        return result;
    }

    private static string Humanize(string value) {
        var words = value
            .Split(['_', '-'], StringSplitOptions.RemoveEmptyEntries)
            .Select(word => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(word.ToLowerInvariant()));
        return string.Join(' ', words);
    }
}

using Prismedia.Contracts.Reader;

namespace Prismedia.Application.Reader;

/// <summary>Outcome status for a reader-lookup use case call.</summary>
public enum ReaderLookupStatus {
    /// <summary>The lookup succeeded.</summary>
    Ok,

    /// <summary>The request was invalid (empty term, text too long, missing target language, …).</summary>
    Invalid,

    /// <summary>The provider had nothing for this term (a clean 404, not a provider failure).</summary>
    NotFound,

    /// <summary>The upstream provider failed (network error, timeout, malformed response).</summary>
    ProviderUnavailable
}

/// <summary>Result envelope shared by every reader-lookup use case.</summary>
/// <typeparam name="T">The response payload type.</typeparam>
/// <param name="Status">What happened.</param>
/// <param name="Value">The payload, present only when <see cref="Status"/> is <see cref="ReaderLookupStatus.Ok"/>.</param>
/// <param name="Error">Human-readable detail for a non-Ok status.</param>
public sealed record ReaderLookupOutcome<T>(ReaderLookupStatus Status, T? Value, string? Error) {
    public static ReaderLookupOutcome<T> Ok(T value) => new(ReaderLookupStatus.Ok, value, null);
    public static ReaderLookupOutcome<T> Invalid(string error) => new(ReaderLookupStatus.Invalid, default, error);
    public static ReaderLookupOutcome<T> NotFound() => new(ReaderLookupStatus.NotFound, default, null);
    public static ReaderLookupOutcome<T> Unavailable(string error) =>
        new(ReaderLookupStatus.ProviderUnavailable, default, error);
}

/// <summary>
/// Application use-case service for the reader's Define/Translate/Wikipedia lookups. Validates
/// input, applies shared defaults (default language, translation text length), and delegates to
/// the configured provider ports.
/// </summary>
public sealed class ReaderLookupService {
    /// <summary>Maximum characters accepted for a single translation request.</summary>
    public const int MaxTranslationLength = 500;

    /// <summary>Default lookup language when the caller does not specify one.</summary>
    public const string DefaultLanguage = "en";

    private readonly IDictionaryProvider _dictionary;
    private readonly ITranslationProvider _translation;
    private readonly IEncyclopediaProvider _encyclopedia;

    public ReaderLookupService(
        IDictionaryProvider dictionary,
        ITranslationProvider translation,
        IEncyclopediaProvider encyclopedia) {
        _dictionary = dictionary;
        _translation = translation;
        _encyclopedia = encyclopedia;
    }

    /// <summary>Looks up a dictionary definition for one term.</summary>
    public async Task<ReaderLookupOutcome<DictionaryDefinitionResponse>> DefineAsync(
        string? term, string? language, CancellationToken cancellationToken) {
        var normalizedTerm = NormalizeTerm(term);
        if (normalizedTerm is null) {
            return ReaderLookupOutcome<DictionaryDefinitionResponse>.Invalid("A term is required.");
        }

        var result = await _dictionary.DefineAsync(
            normalizedTerm, NormalizeLanguage(language), cancellationToken);
        return ToOutcome(result);
    }

    /// <summary>Translates a passage of text.</summary>
    public async Task<ReaderLookupOutcome<TranslateTextResponse>> TranslateAsync(
        TranslateTextRequest? request, CancellationToken cancellationToken) {
        if (request is null || string.IsNullOrWhiteSpace(request.Text)) {
            return ReaderLookupOutcome<TranslateTextResponse>.Invalid("Text to translate is required.");
        }

        var text = request.Text.Trim();
        if (text.Length > MaxTranslationLength) {
            return ReaderLookupOutcome<TranslateTextResponse>.Invalid(
                $"Text must be at most {MaxTranslationLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(request.TargetLanguage)) {
            return ReaderLookupOutcome<TranslateTextResponse>.Invalid("A target language is required.");
        }

        var result = await _translation.TranslateAsync(
            text,
            string.IsNullOrWhiteSpace(request.SourceLanguage) ? null : request.SourceLanguage.Trim(),
            request.TargetLanguage.Trim(),
            cancellationToken);
        return ToOutcome(result);
    }

    /// <summary>Looks up a Wikipedia summary for one term/title.</summary>
    public async Task<ReaderLookupOutcome<WikipediaSummaryResponse>> WikipediaAsync(
        string? term, string? language, CancellationToken cancellationToken) {
        var normalizedTerm = NormalizeTerm(term);
        if (normalizedTerm is null) {
            return ReaderLookupOutcome<WikipediaSummaryResponse>.Invalid("A term is required.");
        }

        var result = await _encyclopedia.SummarizeAsync(
            normalizedTerm, NormalizeLanguage(language), cancellationToken);
        return ToOutcome(result);
    }

    private static ReaderLookupOutcome<T> ToOutcome<T>(ReaderLookupResult<T> result) {
        if (result.ProviderFailed) {
            return ReaderLookupOutcome<T>.Unavailable(result.Error!);
        }

        return result.Found ? ReaderLookupOutcome<T>.Ok(result.Value!) : ReaderLookupOutcome<T>.NotFound();
    }

    private static string? NormalizeTerm(string? term) {
        var trimmed = term?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static string NormalizeLanguage(string? language) =>
        string.IsNullOrWhiteSpace(language) ? DefaultLanguage : language.Trim().ToLowerInvariant();
}

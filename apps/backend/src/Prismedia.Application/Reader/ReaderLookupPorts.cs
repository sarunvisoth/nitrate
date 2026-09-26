using Prismedia.Contracts.Reader;

namespace Prismedia.Application.Reader;

/// <summary>Outcome of a reader-lookup provider call.</summary>
/// <typeparam name="T">The successful response payload type.</typeparam>
public readonly struct ReaderLookupResult<T> {
    private ReaderLookupResult(bool found, T? value, string? error) {
        Found = found;
        Value = value;
        Error = error;
    }

    /// <summary>True when the provider returned a usable result.</summary>
    public bool Found { get; }

    /// <summary>The payload, when <see cref="Found"/> is true.</summary>
    public T? Value { get; }

    /// <summary>
    /// True when the provider itself failed (network error, timeout, malformed response) as
    /// opposed to a clean "no entry for this term" answer. Endpoints map this to a 502-equivalent
    /// <c>ApiProblem</c>; a clean not-found maps to 404.
    /// </summary>
    public bool ProviderFailed => Error is not null;

    /// <summary>Human-readable failure reason, set only when <see cref="ProviderFailed"/> is true.</summary>
    public string? Error { get; }

    /// <summary>A successful result carrying a payload.</summary>
    public static ReaderLookupResult<T> Ok(T value) => new(true, value, null);

    /// <summary>A clean "nothing found for this term" result — not a provider failure.</summary>
    public static ReaderLookupResult<T> NotFound() => new(false, default, null);

    /// <summary>A provider failure (network error, timeout, bad response).</summary>
    public static ReaderLookupResult<T> Failed(string error) => new(false, default, error);
}

/// <summary>
/// Adapter port for dictionary/definition lookups. Implemented in Infrastructure against a
/// concrete provider (default: Wiktionary's REST API).
/// </summary>
public interface IDictionaryProvider {
    /// <summary>Looks up a term's definitions in the given language.</summary>
    Task<ReaderLookupResult<DictionaryDefinitionResponse>> DefineAsync(
        string term, string language, CancellationToken cancellationToken);
}

/// <summary>
/// Adapter port for short text translation. Implemented in Infrastructure against a configurable
/// provider (default: MyMemory; optionally a self-hosted LibreTranslate instance).
/// </summary>
public interface ITranslationProvider {
    /// <summary>Translates <paramref name="text"/> into <paramref name="targetLanguage"/>.</summary>
    Task<ReaderLookupResult<TranslateTextResponse>> TranslateAsync(
        string text, string? sourceLanguage, string targetLanguage, CancellationToken cancellationToken);
}

/// <summary>
/// Adapter port for encyclopedia summary lookups. Implemented in Infrastructure against
/// Wikipedia's REST summary API.
/// </summary>
public interface IEncyclopediaProvider {
    /// <summary>Looks up a Wikipedia summary for a term/title in the given language.</summary>
    Task<ReaderLookupResult<WikipediaSummaryResponse>> SummarizeAsync(
        string term, string language, CancellationToken cancellationToken);
}

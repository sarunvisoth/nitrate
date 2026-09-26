namespace Prismedia.Contracts.Reader;

/// <summary>
/// One part-of-speech group's definitions, as returned by a dictionary provider.
/// </summary>
/// <param name="PartOfSpeech">Human-readable part of speech (e.g. "noun", "verb"), or null when unknown.</param>
/// <param name="Definitions">Definitions for this part of speech, in provider order.</param>
public sealed record DictionaryEntry(string? PartOfSpeech, IReadOnlyList<DictionaryDefinition> Definitions);

/// <summary>
/// One definition and its usage examples, with all markup already stripped.
/// </summary>
/// <param name="Text">Plain-text definition.</param>
/// <param name="Examples">Plain-text usage examples, if the provider included any.</param>
public sealed record DictionaryDefinition(string Text, IReadOnlyList<string> Examples);

/// <summary>
/// Response for <c>GET /api/reader/define</c>.
/// </summary>
/// <param name="Term">The looked-up term, as sent by the caller.</param>
/// <param name="Language">The two-letter language code the definition was resolved for.</param>
/// <param name="Entries">Definitions grouped by part of speech; empty when nothing was found.</param>
/// <param name="SourceUrl">Link to the provider's own page for this term.</param>
/// <param name="Provider">Which provider answered this lookup.</param>
/// <param name="Attribution">Human-readable attribution text to show alongside the definitions.</param>
public sealed record DictionaryDefinitionResponse(
    string Term,
    string Language,
    IReadOnlyList<DictionaryEntry> Entries,
    string SourceUrl,
    Prismedia.Domain.Entities.ReaderProviderKind Provider,
    string Attribution);

/// <summary>
/// Request body for <c>POST /api/reader/translate</c>.
/// </summary>
/// <param name="Text">Text to translate (at most 500 characters).</param>
/// <param name="SourceLanguage">Optional source language code; omitted lets the provider autodetect.</param>
/// <param name="TargetLanguage">Target language code.</param>
public sealed record TranslateTextRequest(string Text, string? SourceLanguage, string TargetLanguage);

/// <summary>
/// Response for <c>POST /api/reader/translate</c>.
/// </summary>
/// <param name="Text">The original text that was translated.</param>
/// <param name="TranslatedText">The translated text.</param>
/// <param name="SourceLanguage">Resolved source language code (detected or provided).</param>
/// <param name="TargetLanguage">Target language code.</param>
/// <param name="Provider">Which provider answered this translation.</param>
public sealed record TranslateTextResponse(
    string Text,
    string TranslatedText,
    string SourceLanguage,
    string TargetLanguage,
    Prismedia.Domain.Entities.ReaderProviderKind Provider);

/// <summary>
/// Response for <c>GET /api/reader/wikipedia</c>.
/// </summary>
/// <param name="Title">The resolved article title.</param>
/// <param name="Extract">Plain-text summary extract.</param>
/// <param name="Url">Link to the full article.</param>
/// <param name="ThumbnailUrl">Optional thumbnail image URL.</param>
public sealed record WikipediaSummaryResponse(string Title, string Extract, string Url, string? ThumbnailUrl);

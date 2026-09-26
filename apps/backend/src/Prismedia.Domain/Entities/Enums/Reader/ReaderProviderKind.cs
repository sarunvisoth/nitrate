namespace Prismedia.Domain.Entities;

/// <summary>
/// Closed set of external reader-lookup providers Nitrate can proxy for the EPUB reader's
/// Define/Translate/Wikipedia actions. This is server-internal wire vocabulary (it labels which
/// provider answered a lookup for attribution purposes); the reader itself does not let the user
/// choose a provider in M5, so this enum is not surfaced to the frontend codegen.
/// </summary>
public enum ReaderProviderKind {
    /// <summary>English Wiktionary REST API (dictionary definitions).</summary>
    [Code("wiktionary")]
    Wiktionary,

    /// <summary>Wikipedia REST summary API (encyclopedia lookups).</summary>
    [Code("wikipedia")]
    Wikipedia,

    /// <summary>MyMemory translation API — the default translation provider.</summary>
    [Code("mymemory")]
    MyMemory,

    /// <summary>Self-hosted LibreTranslate instance, used when configured in settings.</summary>
    [Code("libretranslate")]
    LibreTranslate
}

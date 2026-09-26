namespace Prismedia.Contracts.Reader;

/// <summary>The current user's reader typography, theme, flow, and translation defaults.</summary>
/// <param name="FontFamily">Font family code.</param>
/// <param name="FontSizePercent">Font size as a percentage (80-200).</param>
/// <param name="LineHeight">Line-height multiplier (1.2-2.0).</param>
/// <param name="MarginStep">Side-margin step (0-4; 0 is narrowest).</param>
/// <param name="MaxContentWidth">Maximum reading column width in pixels (320-1200).</param>
/// <param name="Theme">Theme code.</param>
/// <param name="Flow">Default reading flow code (paged or scrolled; webtoon is comics-only).</param>
/// <param name="TranslationTargetLanguage">Default translate-to language code, or null.</param>
/// <param name="UpdatedAt">Last-write-wins clock, or null when these are unsaved defaults.</param>
public sealed record ReaderPreferencesResponse(
    string FontFamily,
    int FontSizePercent,
    double LineHeight,
    int MarginStep,
    int MaxContentWidth,
    string Theme,
    string Flow,
    string? TranslationTargetLanguage,
    DateTimeOffset? UpdatedAt);

/// <summary>Write payload for the current user's reader preferences.</summary>
/// <param name="FontFamily">Font family code.</param>
/// <param name="FontSizePercent">Font size as a percentage (80-200).</param>
/// <param name="LineHeight">Line-height multiplier (1.2-2.0).</param>
/// <param name="MarginStep">Side-margin step (0-4).</param>
/// <param name="MaxContentWidth">Maximum reading column width in pixels (320-1200).</param>
/// <param name="Theme">Theme code.</param>
/// <param name="Flow">Default reading flow code (paged or scrolled).</param>
/// <param name="TranslationTargetLanguage">Default translate-to language code, or null to clear it.</param>
/// <param name="UpdatedAt">Client clock for last-write-wins.</param>
public sealed record UpsertReaderPreferencesRequest(
    string FontFamily,
    int FontSizePercent,
    double LineHeight,
    int MarginStep,
    int MaxContentWidth,
    string Theme,
    string Flow,
    string? TranslationTargetLanguage,
    DateTimeOffset UpdatedAt);

/// <summary>Outcome of a reader-preferences write.</summary>
public enum ReaderPreferencesWriteStatus {
    /// <summary>The write was stored.</summary>
    Applied,

    /// <summary>A newer stored version won; the result carries that version.</summary>
    Superseded,

    /// <summary>The payload failed validation.</summary>
    Invalid
}

/// <summary>Result of a reader-preferences write.</summary>
/// <param name="Status">The outcome category.</param>
/// <param name="Preferences">The stored preferences after the write.</param>
/// <param name="Error">A user-facing validation message when invalid.</param>
public sealed record ReaderPreferencesWriteResult(
    ReaderPreferencesWriteStatus Status,
    ReaderPreferencesResponse? Preferences,
    string? Error);

namespace Prismedia.Contracts.Vocabulary;

/// <summary>One saved vocabulary word.</summary>
/// <param name="Id">Client-generated stable identifier.</param>
/// <param name="Word">The saved word or phrase.</param>
/// <param name="Language">BCP-47-ish language code the word is in.</param>
/// <param name="Definition">Captured definition, or null.</param>
/// <param name="BookId">Book Entity the word was found in, or null.</param>
/// <param name="BookTitle">Title of that Book, or null when there is none or it is no longer visible.</param>
/// <param name="SourceLocator">Format-specific locator (e.g. an EPUB CFI) to jump back to the word.</param>
/// <param name="SourceSentence">Up to 1000 characters of surrounding context.</param>
/// <param name="Status">Learning-progress status code.</param>
/// <param name="CreatedAt">When the word was first saved.</param>
/// <param name="UpdatedAt">Last-write-wins clock.</param>
public sealed record VocabularyEntry(
    Guid Id,
    string Word,
    string Language,
    string? Definition,
    Guid? BookId,
    string? BookTitle,
    string? SourceLocator,
    string? SourceSentence,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>A page of the current user's vocabulary list.</summary>
/// <param name="Items">Entries on this page, newest first.</param>
/// <param name="NextCursor">Opaque cursor for the next page, or null at the end.</param>
/// <param name="TotalCount">Total matching entries across all pages.</param>
public sealed record VocabularyListResponse(
    IReadOnlyList<VocabularyEntry> Items,
    string? NextCursor,
    int TotalCount);

/// <summary>Create-or-update payload for one vocabulary word.</summary>
/// <param name="Word">The word or phrase to save.</param>
/// <param name="Language">Language code the word is in.</param>
/// <param name="Definition">Captured definition, or null to clear it.</param>
/// <param name="BookId">Book Entity the word was found in, or null.</param>
/// <param name="SourceLocator">Format-specific locator to jump back to the word.</param>
/// <param name="SourceSentence">Up to 1000 characters of surrounding context.</param>
/// <param name="Status">Learning-progress status code.</param>
/// <param name="UpdatedAt">Client clock for last-write-wins.</param>
public sealed record UpsertVocabularyEntryRequest(
    string Word,
    string Language,
    string? Definition,
    Guid? BookId,
    string? SourceLocator,
    string? SourceSentence,
    string Status,
    DateTimeOffset UpdatedAt);

/// <summary>Outcome of a vocabulary write.</summary>
public enum VocabularyWriteStatus {
    /// <summary>The write was stored (created or updated).</summary>
    Applied,

    /// <summary>A newer stored version won; the result carries that version.</summary>
    Superseded,

    /// <summary>No live entry with that id belongs to the caller.</summary>
    NotFound,

    /// <summary>The payload failed validation.</summary>
    Invalid
}

/// <summary>Result of a vocabulary write.</summary>
/// <param name="Status">The outcome category.</param>
/// <param name="Entry">The stored entry after the write, when found.</param>
/// <param name="Error">A user-facing validation message when invalid.</param>
public sealed record VocabularyWriteResult(
    VocabularyWriteStatus Status,
    VocabularyEntry? Entry,
    string? Error);

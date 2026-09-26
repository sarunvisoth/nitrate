using Prismedia.Contracts.Vocabulary;
using Prismedia.Domain.Entities;

namespace Prismedia.Application.Vocabulary;

/// <summary>Filters for the current user's vocabulary list.</summary>
/// <param name="Search">Case-insensitive text matched against the word and definition.</param>
/// <param name="Status">Restrict to one status.</param>
/// <param name="BookId">Restrict to one Book.</param>
/// <param name="Cursor">Opaque page cursor from a previous response.</param>
/// <param name="Limit">Page size.</param>
public sealed record VocabularyQuery(
    string? Search,
    VocabularyStatus? Status,
    Guid? BookId,
    string? Cursor,
    int Limit);

/// <summary>An export ready to download.</summary>
/// <param name="Content">File bytes.</param>
/// <param name="ContentType">MIME type.</param>
/// <param name="FileName">Suggested download file name.</param>
public sealed record VocabularyExportFile(byte[] Content, string ContentType, string FileName);

/// <summary>
/// Reads and writes the current user's saved vocabulary words. Adding a word already saved
/// (same word, case-insensitive, and language) updates that entry instead of duplicating it.
/// </summary>
public interface IVocabularyService {
    /// <summary>Lists matching live entries, newest first.</summary>
    Task<VocabularyListResponse> ListAsync(VocabularyQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Creates or updates the entry named by <paramref name="entryId"/>; when the word/language pair
    /// already exists on a different live entry, that existing entry is updated and returned instead.
    /// </summary>
    Task<VocabularyWriteResult> UpsertAsync(
        Guid entryId,
        UpsertVocabularyEntryRequest request,
        CancellationToken cancellationToken);

    /// <summary>Tombstones one entry under the same last-write-wins rule.</summary>
    Task<VocabularyWriteResult> DeleteAsync(Guid entryId, DateTimeOffset updatedAt, CancellationToken cancellationToken);

    /// <summary>Exports the caller's live vocabulary (optionally filtered) in the requested format.</summary>
    Task<VocabularyExportFile> ExportAsync(
        AnnotationExportFormat format,
        VocabularyQuery query,
        CancellationToken cancellationToken);
}

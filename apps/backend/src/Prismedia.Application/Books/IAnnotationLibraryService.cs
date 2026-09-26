using Prismedia.Contracts.Books;
using Prismedia.Domain.Entities;

namespace Prismedia.Application.Books;

/// <summary>Filters for the cross-library annotation list.</summary>
/// <param name="Search">Case-insensitive text matched against highlighted text and notes.</param>
/// <param name="BookId">Restrict to one Book.</param>
/// <param name="Color">Restrict to one highlight colour.</param>
/// <param name="NotesOnly">Only highlights that carry a note.</param>
/// <param name="Cursor">Opaque page cursor from a previous response.</param>
/// <param name="Limit">Page size.</param>
public sealed record AnnotationLibraryQuery(
    string? Search,
    Guid? BookId,
    AnnotationColor? Color,
    bool NotesOnly,
    string? Cursor,
    int Limit);

/// <summary>An export ready to download.</summary>
/// <param name="Content">File bytes.</param>
/// <param name="ContentType">MIME type.</param>
/// <param name="FileName">Suggested download file name.</param>
public sealed record AnnotationExportFile(byte[] Content, string ContentType, string FileName);

/// <summary>
/// Reads the current user's live highlights and notes across every visible Book, for the dashboard and export.
/// </summary>
public interface IAnnotationLibraryService {
    /// <summary>Lists matching highlights ordered by book title, then reading position.</summary>
    Task<AnnotationLibraryResponse> ListAsync(AnnotationLibraryQuery query, CancellationToken cancellationToken);

    /// <summary>Lists every book with highlights, with counts.</summary>
    Task<AnnotationLibraryBooksResponse> BooksAsync(CancellationToken cancellationToken);

    /// <summary>Exports highlights (optionally for one Book) in the requested format.</summary>
    Task<AnnotationExportFile> ExportAsync(
        AnnotationExportFormat format,
        Guid? bookId,
        CancellationToken cancellationToken);
}

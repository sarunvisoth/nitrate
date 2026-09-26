using Prismedia.Contracts.Books;

namespace Prismedia.Application.Books;

/// <summary>
/// Reads and writes the current user's annotations on a visible Book with last-write-wins semantics.
/// </summary>
public interface IBookAnnotationService {
    /// <summary>Lists the current user's annotations for a visible Book, including tombstones.</summary>
    /// <param name="bookId">Identifier of the Book Entity.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>The annotations, or <c>null</c> when the Book is missing, hidden, or there is no user.</returns>
    Task<BookAnnotationsResponse?> ListAsync(Guid bookId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates or updates one annotation; applied only when the incoming clock is not older than the stored one.
    /// </summary>
    /// <param name="bookId">Identifier of the Book Entity.</param>
    /// <param name="annotationId">Client-generated annotation identifier.</param>
    /// <param name="request">The desired annotation state.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>The write outcome and the resulting stored annotation.</returns>
    Task<BookAnnotationWriteResult> UpsertAsync(
        Guid bookId,
        Guid annotationId,
        UpsertBookAnnotationRequest request,
        CancellationToken cancellationToken);

    /// <summary>Tombstones one annotation under the same last-write-wins rule.</summary>
    /// <param name="bookId">Identifier of the Book Entity.</param>
    /// <param name="annotationId">Annotation identifier.</param>
    /// <param name="updatedAt">Client clock for last-write-wins.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>The write outcome and the resulting stored annotation.</returns>
    Task<BookAnnotationWriteResult> DeleteAsync(
        Guid bookId,
        Guid annotationId,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken);
}

/// <summary>Outcome of an annotation write.</summary>
public enum BookAnnotationWriteStatus {
    /// <summary>The write was stored.</summary>
    Applied,

    /// <summary>A newer stored version won; the result carries that version.</summary>
    Superseded,

    /// <summary>The Book is missing or hidden, there is no user, or the annotation belongs to another user or Book.</summary>
    NotFound,

    /// <summary>The payload failed validation.</summary>
    Invalid
}

/// <summary>Result of an annotation write.</summary>
/// <param name="Status">The outcome category.</param>
/// <param name="Annotation">The stored annotation after the write (applied or superseding), when found.</param>
/// <param name="Error">A user-facing validation message when invalid.</param>
public sealed record BookAnnotationWriteResult(
    BookAnnotationWriteStatus Status,
    BookAnnotation? Annotation,
    string? Error);

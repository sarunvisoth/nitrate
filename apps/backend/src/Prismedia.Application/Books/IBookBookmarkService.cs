using Prismedia.Contracts.Books;

namespace Prismedia.Application.Books;

/// <summary>
/// Reads and writes the current user's bookmarks on a visible Book, with the same last-write-wins and
/// tombstone semantics as annotations (<see cref="BookAnnotationWritePolicy"/>).
/// </summary>
public interface IBookBookmarkService {
    /// <summary>Lists the current user's bookmarks for a visible Book, including tombstones.</summary>
    /// <returns>The bookmarks, or <c>null</c> when the Book is missing, hidden, or there is no user.</returns>
    Task<BookBookmarksResponse?> ListAsync(Guid bookId, CancellationToken cancellationToken);

    /// <summary>Creates or updates one bookmark; applied only when the incoming clock is not older.</summary>
    Task<BookBookmarkWriteResult> UpsertAsync(
        Guid bookId,
        Guid bookmarkId,
        UpsertBookBookmarkRequest request,
        CancellationToken cancellationToken);

    /// <summary>Tombstones one bookmark under the same last-write-wins rule.</summary>
    Task<BookBookmarkWriteResult> DeleteAsync(
        Guid bookId,
        Guid bookmarkId,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken);
}

/// <summary>Result of a bookmark write.</summary>
/// <param name="Status">The outcome category (shared with annotation writes).</param>
/// <param name="Bookmark">The stored bookmark after the write (applied or superseding), when found.</param>
/// <param name="Error">A user-facing validation message when invalid.</param>
public sealed record BookBookmarkWriteResult(
    BookAnnotationWriteStatus Status,
    BookBookmark? Bookmark,
    string? Error);

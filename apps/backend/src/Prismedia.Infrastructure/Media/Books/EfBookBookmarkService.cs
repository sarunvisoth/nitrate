using Microsoft.EntityFrameworkCore;
using Prismedia.Application.Books;
using Prismedia.Application.Entities;
using Prismedia.Application.Security;
using Prismedia.Contracts.Books;
using Prismedia.Domain.Entities;
using Prismedia.Infrastructure.Persistence;
using Prismedia.Infrastructure.Persistence.Entities;

namespace Prismedia.Infrastructure.Media.Books;

/// <summary>
/// EF store for per-user Book bookmarks with last-write-wins writes and tombstoned deletes.
/// Hidden Books, non-Book entities, anonymous callers, and other users' bookmarks all behave as missing.
/// </summary>
internal sealed class EfBookBookmarkService(
    PrismediaDbContext db,
    IEntityVisibilityChecker visibility,
    ICurrentUserContext currentUser,
    TimeProvider clock) : IBookBookmarkService {
    private const int LocatorMaxLength = 4096;
    private const int LabelMaxLength = 512;

    /// <inheritdoc />
    public async Task<BookBookmarksResponse?> ListAsync(Guid bookId, CancellationToken cancellationToken) {
        if (currentUser.UserId == Guid.Empty || !await IsVisibleBookAsync(bookId, cancellationToken)) {
            return null;
        }

        var rows = await db.BookBookmarks.AsNoTracking()
            .Where(row => row.UserId == currentUser.UserId && row.BookId == bookId)
            .OrderBy(row => row.SpineIndex)
            .ThenBy(row => row.Position)
            .ThenBy(row => row.CreatedAt)
            .ToListAsync(cancellationToken);
        return new BookBookmarksResponse(rows.Select(ToContract).ToList());
    }

    /// <inheritdoc />
    public async Task<BookBookmarkWriteResult> UpsertAsync(
        Guid bookId,
        Guid bookmarkId,
        UpsertBookBookmarkRequest request,
        CancellationToken cancellationToken) {
        var (found, row) = await LoadOwnedAsync(bookId, bookmarkId, cancellationToken);
        if (!found) {
            return NotFound();
        }

        var error = Validate(request);
        if (error is not null) {
            return new BookBookmarkWriteResult(BookAnnotationWriteStatus.Invalid, null, error);
        }

        if (row is not null && !BookAnnotationWritePolicy.ShouldApply(row.UpdatedAt, request.UpdatedAt)) {
            return new BookBookmarkWriteResult(BookAnnotationWriteStatus.Superseded, ToContract(row), null);
        }

        if (row is null) {
            row = new BookBookmarkRow {
                Id = bookmarkId,
                UserId = currentUser.UserId,
                BookId = bookId,
                CreatedAt = clock.GetUtcNow()
            };
            db.BookBookmarks.Add(row);
        }

        row.LocatorKind = request.LocatorKind.DecodeAs<AnnotationLocatorKind>();
        row.Locator = request.Locator.Trim();
        row.SpineIndex = request.SpineIndex;
        row.Position = request.Position;
        row.ChapterLabel = Clip(request.ChapterLabel);
        row.Label = Clip(request.Label);
        row.UpdatedAt = request.UpdatedAt;
        row.DeletedAt = null;
        await db.SaveChangesAsync(cancellationToken);
        return new BookBookmarkWriteResult(BookAnnotationWriteStatus.Applied, ToContract(row), null);
    }

    /// <inheritdoc />
    public async Task<BookBookmarkWriteResult> DeleteAsync(
        Guid bookId,
        Guid bookmarkId,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken) {
        var (found, row) = await LoadOwnedAsync(bookId, bookmarkId, cancellationToken);
        if (!found || row is null) {
            return NotFound();
        }

        if (!BookAnnotationWritePolicy.IsPlausibleClock(updatedAt, clock.GetUtcNow())) {
            return new BookBookmarkWriteResult(
                BookAnnotationWriteStatus.Invalid,
                null,
                "updatedAt is too far in the future.");
        }

        if (!BookAnnotationWritePolicy.ShouldApply(row.UpdatedAt, updatedAt)) {
            return new BookBookmarkWriteResult(BookAnnotationWriteStatus.Superseded, ToContract(row), null);
        }

        row.UpdatedAt = updatedAt;
        row.DeletedAt = updatedAt;
        await db.SaveChangesAsync(cancellationToken);
        return new BookBookmarkWriteResult(BookAnnotationWriteStatus.Applied, ToContract(row), null);
    }

    private async Task<(bool Found, BookBookmarkRow? Row)> LoadOwnedAsync(
        Guid bookId,
        Guid bookmarkId,
        CancellationToken cancellationToken) {
        if (currentUser.UserId == Guid.Empty ||
            bookmarkId == Guid.Empty ||
            !await IsVisibleBookAsync(bookId, cancellationToken)) {
            return (false, null);
        }

        var row = await db.BookBookmarks.FirstOrDefaultAsync(r => r.Id == bookmarkId, cancellationToken);
        if (row is not null && (row.UserId != currentUser.UserId || row.BookId != bookId)) {
            return (false, null);
        }

        return (true, row);
    }

    private string? Validate(UpsertBookBookmarkRequest request) {
        if (!request.LocatorKind.TryDecodeAs<AnnotationLocatorKind>(out _)) {
            return "Unknown locatorKind.";
        }

        if (string.IsNullOrWhiteSpace(request.Locator) || request.Locator.Length > LocatorMaxLength) {
            return $"locator is required (max {LocatorMaxLength} characters).";
        }

        if (request.SpineIndex < 0) {
            return "spineIndex must be zero or greater.";
        }

        if (double.IsNaN(request.Position) || request.Position is < 0 or > 1) {
            return "position must be within 0..1.";
        }

        return BookAnnotationWritePolicy.IsPlausibleClock(request.UpdatedAt, clock.GetUtcNow())
            ? null
            : "updatedAt is too far in the future.";
    }

    private async Task<bool> IsVisibleBookAsync(Guid bookId, CancellationToken cancellationToken) =>
        await visibility.IsVisibleAsync(bookId, cancellationToken) &&
        await db.Entities.AsNoTracking().AnyAsync(
            row => row.Id == bookId && row.KindCode == EntityKind.Book.ToCode(),
            cancellationToken);

    private static BookBookmarkWriteResult NotFound() =>
        new(BookAnnotationWriteStatus.NotFound, null, null);

    private static string? Clip(string? value) {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) {
            return null;
        }

        return trimmed.Length <= LabelMaxLength ? trimmed : trimmed[..LabelMaxLength];
    }

    private static BookBookmark ToContract(BookBookmarkRow row) => new(
        row.Id,
        row.BookId,
        row.LocatorKind.ToCode(),
        row.Locator,
        row.SpineIndex,
        row.Position,
        row.ChapterLabel,
        row.Label,
        row.CreatedAt,
        row.UpdatedAt,
        row.DeletedAt);
}

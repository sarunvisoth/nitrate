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
/// EF store for per-user Book annotations with last-write-wins writes and tombstoned deletes.
/// Hidden Books, non-Book entities, anonymous callers, and other users' annotations all behave as missing.
/// </summary>
internal sealed class EfBookAnnotationService(
    PrismediaDbContext db,
    IEntityVisibilityChecker visibility,
    ICurrentUserContext currentUser,
    TimeProvider clock) : IBookAnnotationService {
    private const int ContextMaxLength = 256;
    private const int LocatorMaxLength = 4096;
    private const int SelectedTextMaxLength = 8000;
    private const int NoteMaxLength = 20000;

    /// <inheritdoc />
    public async Task<BookAnnotationsResponse?> ListAsync(Guid bookId, CancellationToken cancellationToken) {
        if (currentUser.UserId == Guid.Empty || !await IsVisibleBookAsync(bookId, cancellationToken)) {
            return null;
        }

        var rows = await db.BookAnnotations.AsNoTracking()
            .Where(row => row.UserId == currentUser.UserId && row.BookId == bookId)
            .OrderBy(row => row.SpineIndex)
            .ThenBy(row => row.Position)
            .ThenBy(row => row.CreatedAt)
            .ToListAsync(cancellationToken);
        return new BookAnnotationsResponse(rows.Select(ToContract).ToList());
    }

    /// <inheritdoc />
    public async Task<BookAnnotationWriteResult> UpsertAsync(
        Guid bookId,
        Guid annotationId,
        UpsertBookAnnotationRequest request,
        CancellationToken cancellationToken) {
        var (found, row) = await LoadOwnedAsync(bookId, annotationId, cancellationToken);
        if (!found) {
            return NotFound();
        }

        var error = Validate(request);
        if (error is not null) {
            return new BookAnnotationWriteResult(BookAnnotationWriteStatus.Invalid, null, error);
        }

        if (row is not null && !BookAnnotationWritePolicy.ShouldApply(row.UpdatedAt, request.UpdatedAt)) {
            return new BookAnnotationWriteResult(BookAnnotationWriteStatus.Superseded, ToContract(row), null);
        }

        if (row is null) {
            row = new BookAnnotationRow {
                Id = annotationId,
                UserId = currentUser.UserId,
                BookId = bookId,
                CreatedAt = clock.GetUtcNow(),
                SourceSignature = await db.BookContentStates.AsNoTracking()
                    .Where(state => state.BookId == bookId)
                    .Select(state => state.SourceSignature)
                    .FirstOrDefaultAsync(cancellationToken)
            };
            db.BookAnnotations.Add(row);
        }

        row.LocatorKind = request.LocatorKind.DecodeAs<AnnotationLocatorKind>();
        row.Locator = request.Locator.Trim();
        row.SpineIndex = request.SpineIndex;
        row.Position = request.Position;
        row.SelectedText = request.SelectedText;
        row.TextBefore = Clip(request.TextBefore);
        row.TextAfter = Clip(request.TextAfter);
        row.Style = request.Style.DecodeAs<AnnotationStyle>();
        row.Color = request.Color.DecodeAs<AnnotationColor>();
        row.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note;
        row.UpdatedAt = request.UpdatedAt;
        row.DeletedAt = null;
        await db.SaveChangesAsync(cancellationToken);
        return new BookAnnotationWriteResult(BookAnnotationWriteStatus.Applied, ToContract(row), null);
    }

    /// <inheritdoc />
    public async Task<BookAnnotationWriteResult> DeleteAsync(
        Guid bookId,
        Guid annotationId,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken) {
        var (found, row) = await LoadOwnedAsync(bookId, annotationId, cancellationToken);
        if (!found || row is null) {
            return NotFound();
        }

        if (!BookAnnotationWritePolicy.IsPlausibleClock(updatedAt, clock.GetUtcNow())) {
            return new BookAnnotationWriteResult(
                BookAnnotationWriteStatus.Invalid,
                null,
                "updatedAt is too far in the future.");
        }

        if (!BookAnnotationWritePolicy.ShouldApply(row.UpdatedAt, updatedAt)) {
            return new BookAnnotationWriteResult(BookAnnotationWriteStatus.Superseded, ToContract(row), null);
        }

        row.UpdatedAt = updatedAt;
        row.DeletedAt = updatedAt;
        await db.SaveChangesAsync(cancellationToken);
        return new BookAnnotationWriteResult(BookAnnotationWriteStatus.Applied, ToContract(row), null);
    }

    /// <summary>
    /// Resolves the caller's annotation. <c>Found</c> is false when the caller, Book, or ownership check fails;
    /// a found result with a null row means the id is free for a new annotation.
    /// </summary>
    private async Task<(bool Found, BookAnnotationRow? Row)> LoadOwnedAsync(
        Guid bookId,
        Guid annotationId,
        CancellationToken cancellationToken) {
        if (currentUser.UserId == Guid.Empty ||
            annotationId == Guid.Empty ||
            !await IsVisibleBookAsync(bookId, cancellationToken)) {
            return (false, null);
        }

        var row = await db.BookAnnotations.FirstOrDefaultAsync(r => r.Id == annotationId, cancellationToken);
        if (row is not null && (row.UserId != currentUser.UserId || row.BookId != bookId)) {
            return (false, null);
        }

        return (true, row);
    }

    private string? Validate(UpsertBookAnnotationRequest request) {
        if (!request.LocatorKind.TryDecodeAs<AnnotationLocatorKind>(out _)) {
            return "Unknown locatorKind.";
        }

        if (!request.Style.TryDecodeAs<AnnotationStyle>(out _)) {
            return "Unknown style.";
        }

        if (!request.Color.TryDecodeAs<AnnotationColor>(out _)) {
            return "Unknown color.";
        }

        if (string.IsNullOrWhiteSpace(request.Locator) || request.Locator.Length > LocatorMaxLength) {
            return $"locator is required (max {LocatorMaxLength} characters).";
        }

        if (string.IsNullOrEmpty(request.SelectedText) || request.SelectedText.Length > SelectedTextMaxLength) {
            return $"selectedText is required (max {SelectedTextMaxLength} characters).";
        }

        if (request.SpineIndex < 0) {
            return "spineIndex must be zero or greater.";
        }

        if (double.IsNaN(request.Position) || request.Position is < 0 or > 1) {
            return "position must be within 0..1.";
        }

        if (request.Note is { Length: > NoteMaxLength }) {
            return $"note is too long (max {NoteMaxLength} characters).";
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

    private static BookAnnotationWriteResult NotFound() =>
        new(BookAnnotationWriteStatus.NotFound, null, null);

    private static string? Clip(string? value) =>
        string.IsNullOrEmpty(value) ? null : value.Length <= ContextMaxLength ? value : value[..ContextMaxLength];

    private static BookAnnotation ToContract(BookAnnotationRow row) => new(
        row.Id,
        row.BookId,
        row.LocatorKind.ToCode(),
        row.Locator,
        row.SpineIndex,
        row.Position,
        row.SelectedText,
        row.TextBefore,
        row.TextAfter,
        row.Style.ToCode(),
        row.Color.ToCode(),
        row.Note,
        row.SourceSignature,
        row.CreatedAt,
        row.UpdatedAt,
        row.DeletedAt);
}

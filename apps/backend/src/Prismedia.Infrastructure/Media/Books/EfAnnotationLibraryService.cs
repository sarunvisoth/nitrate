using System.Text;
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
/// EF read side for the annotation dashboard and export: the caller's live highlights on Books they can see,
/// ordered by book title then reading position, with a stale flag when the book's file changed since.
/// </summary>
internal sealed class EfAnnotationLibraryService(
    PrismediaDbContext db,
    IEntityVisibilityChecker visibility,
    ICurrentUserContext currentUser,
    TimeProvider clock) : IAnnotationLibraryService {
    private const int MaximumPageSize = 500;
    private const string CursorPrefix = "offset:";

    /// <inheritdoc />
    public async Task<AnnotationLibraryResponse> ListAsync(
        AnnotationLibraryQuery query,
        CancellationToken cancellationToken) {
        var visibleBooks = await VisibleBookIdsAsync(query.BookId, cancellationToken);
        if (visibleBooks.Count == 0) {
            return new AnnotationLibraryResponse([], null, 0);
        }

        var filtered = LiveAnnotations(visibleBooks);
        if (!string.IsNullOrWhiteSpace(query.Search)) {
            var term = query.Search.Trim().ToLowerInvariant();
            filtered = filtered.Where(row =>
                row.Annotation.SelectedText.ToLower().Contains(term) ||
                (row.Annotation.Note != null && row.Annotation.Note.ToLower().Contains(term)));
        }

        if (query.Color is { } color) {
            filtered = filtered.Where(row => row.Annotation.Color == color);
        }

        if (query.NotesOnly) {
            filtered = filtered.Where(row => row.Annotation.Note != null && row.Annotation.Note != "");
        }

        var total = await filtered.CountAsync(cancellationToken);
        var pageSize = Math.Clamp(query.Limit, 1, MaximumPageSize);
        var offset = DecodeCursor(query.Cursor);
        var rows = await Ordered(filtered).Skip(offset).Take(pageSize + 1).ToListAsync(cancellationToken);
        var items = await ToItemsAsync(rows.Take(pageSize).ToList(), cancellationToken);
        var next = rows.Count > pageSize ? EncodeCursor(offset + pageSize) : null;
        return new AnnotationLibraryResponse(items, next, total);
    }

    /// <inheritdoc />
    public async Task<AnnotationLibraryBooksResponse> BooksAsync(CancellationToken cancellationToken) {
        var visibleBooks = await VisibleBookIdsAsync(null, cancellationToken);
        if (visibleBooks.Count == 0) {
            return new AnnotationLibraryBooksResponse([]);
        }

        var items = await ToItemsAsync(
            await Ordered(LiveAnnotations(visibleBooks)).ToListAsync(cancellationToken),
            cancellationToken);
        var books = items
            .GroupBy(item => item.BookId)
            .Select(group => new AnnotationLibraryBook(
                group.Key,
                group.First().BookTitle,
                group.Count(),
                group.Count(item => !string.IsNullOrWhiteSpace(item.Note)),
                group.Count(item => item.IsStale),
                group.Max(item => item.UpdatedAt)))
            .OrderBy(book => book.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new AnnotationLibraryBooksResponse(books);
    }

    /// <inheritdoc />
    public async Task<AnnotationExportFile> ExportAsync(
        AnnotationExportFormat format,
        Guid? bookId,
        CancellationToken cancellationToken) {
        var visibleBooks = await VisibleBookIdsAsync(bookId, cancellationToken);
        var items = visibleBooks.Count == 0
            ? []
            : await ToItemsAsync(
                await Ordered(LiveAnnotations(visibleBooks)).ToListAsync(cancellationToken),
                cancellationToken);
        var scope = bookId is not null && items.Count > 0 ? items[0].BookTitle : null;
        return AnnotationExportWriter.Write(format, items, clock.GetUtcNow(), scope);
    }

    /// <summary>Projection with settable members so EF can translate ordering on it.</summary>
    private sealed class AnnotationWithTitle {
        public required BookAnnotationRow Annotation { get; init; }
        public required string Title { get; init; }
    }

    private IQueryable<AnnotationWithTitle> LiveAnnotations(IReadOnlyCollection<Guid> bookIds) =>
        from annotation in db.BookAnnotations.AsNoTracking()
        join book in db.Entities.AsNoTracking() on annotation.BookId equals book.Id
        where annotation.UserId == currentUser.UserId &&
              annotation.DeletedAt == null &&
              bookIds.Contains(annotation.BookId)
        select new AnnotationWithTitle { Annotation = annotation, Title = book.Title };

    private static IQueryable<AnnotationWithTitle> Ordered(IQueryable<AnnotationWithTitle> rows) =>
        rows.OrderBy(row => row.Title)
            .ThenBy(row => row.Annotation.BookId)
            .ThenBy(row => row.Annotation.SpineIndex)
            .ThenBy(row => row.Annotation.Position)
            .ThenBy(row => row.Annotation.CreatedAt);

    /// <summary>Books the caller has live highlights on and may see, optionally narrowed to one.</summary>
    private async Task<IReadOnlyCollection<Guid>> VisibleBookIdsAsync(Guid? onlyBook, CancellationToken cancellationToken) {
        if (currentUser.UserId == Guid.Empty) {
            return [];
        }

        var candidates = await db.BookAnnotations.AsNoTracking()
            .Where(row => row.UserId == currentUser.UserId && row.DeletedAt == null)
            .Where(row => onlyBook == null || row.BookId == onlyBook)
            .Select(row => row.BookId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var visible = new List<Guid>(candidates.Count);
        foreach (var bookId in candidates) {
            if (await visibility.IsVisibleAsync(bookId, cancellationToken)) {
                visible.Add(bookId);
            }
        }

        var books = await db.Entities.AsNoTracking()
            .Where(row => visible.Contains(row.Id) && row.KindCode == EntityKind.Book.ToCode())
            .Select(row => row.Id)
            .ToListAsync(cancellationToken);
        return books;
    }

    private async Task<IReadOnlyList<AnnotationLibraryItem>> ToItemsAsync(
        IReadOnlyList<AnnotationWithTitle> rows,
        CancellationToken cancellationToken) {
        var bookIds = rows.Select(row => row.Annotation.BookId).Distinct().ToList();
        var signatures = await db.BookContentStates.AsNoTracking()
            .Where(state => bookIds.Contains(state.BookId))
            .ToDictionaryAsync(state => state.BookId, state => state.SourceSignature, cancellationToken);
        return rows.Select(row => {
            var annotation = row.Annotation;
            var current = signatures.GetValueOrDefault(annotation.BookId);
            var stale = annotation.SourceSignature is not null &&
                current is not null &&
                !string.Equals(annotation.SourceSignature, current, StringComparison.Ordinal);
            return new AnnotationLibraryItem(
                annotation.Id,
                annotation.BookId,
                row.Title,
                annotation.LocatorKind.ToCode(),
                annotation.Locator,
                annotation.SpineIndex,
                annotation.Position,
                annotation.SelectedText,
                annotation.Note,
                annotation.Style.ToCode(),
                annotation.Color.ToCode(),
                stale,
                annotation.CreatedAt,
                annotation.UpdatedAt);
        }).ToList();
    }

    private static string EncodeCursor(int offset) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(CursorPrefix + offset));

    private static int DecodeCursor(string? cursor) {
        if (string.IsNullOrWhiteSpace(cursor)) {
            return 0;
        }

        try {
            var text = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return text.StartsWith(CursorPrefix, StringComparison.Ordinal) &&
                int.TryParse(text[CursorPrefix.Length..], out var offset) && offset > 0
                ? offset
                : 0;
        } catch (FormatException) {
            return 0;
        }
    }
}

using System.Text;
using Microsoft.EntityFrameworkCore;
using Prismedia.Application.Books;
using Prismedia.Application.Security;
using Prismedia.Application.Vocabulary;
using Prismedia.Contracts.Vocabulary;
using Prismedia.Domain.Entities;
using Prismedia.Infrastructure.Persistence;
using Prismedia.Infrastructure.Persistence.Entities;

namespace Prismedia.Infrastructure.Media.Reader;

/// <summary>
/// EF store for the current user's vocabulary list. Adding a word already saved (same word,
/// case-insensitive, and language) among live entries updates that entry instead of duplicating.
/// </summary>
internal sealed class EfVocabularyService(
    PrismediaDbContext db,
    ICurrentUserContext currentUser,
    TimeProvider clock) : IVocabularyService {
    private const int MaximumPageSize = 500;
    private const string CursorPrefix = "offset:";
    private const int WordMaxLength = 256;
    private const int LanguageMaxLength = 16;
    private const int SentenceMaxLength = 1000;

    /// <inheritdoc />
    public async Task<VocabularyListResponse> ListAsync(VocabularyQuery query, CancellationToken cancellationToken) {
        if (currentUser.UserId == Guid.Empty) {
            return new VocabularyListResponse([], null, 0);
        }

        var filtered = Live();
        if (!string.IsNullOrWhiteSpace(query.Search)) {
            var term = query.Search.Trim().ToLowerInvariant();
            filtered = filtered.Where(row =>
                row.Word.ToLower().Contains(term) ||
                (row.Definition != null && row.Definition.ToLower().Contains(term)));
        }

        if (query.Status is { } status) {
            filtered = filtered.Where(row => row.Status == status);
        }

        if (query.BookId is { } bookId) {
            filtered = filtered.Where(row => row.BookId == bookId);
        }

        var total = await filtered.CountAsync(cancellationToken);
        var pageSize = Math.Clamp(query.Limit, 1, MaximumPageSize);
        var offset = DecodeCursor(query.Cursor);
        var rows = await filtered
            .OrderByDescending(row => row.UpdatedAt)
            .ThenBy(row => row.Id)
            .Skip(offset)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);
        var items = await ToItemsAsync(rows.Take(pageSize).ToList(), cancellationToken);
        var next = rows.Count > pageSize ? EncodeCursor(offset + pageSize) : null;
        return new VocabularyListResponse(items, next, total);
    }

    /// <inheritdoc />
    public async Task<VocabularyWriteResult> UpsertAsync(
        Guid entryId,
        UpsertVocabularyEntryRequest request,
        CancellationToken cancellationToken) {
        if (currentUser.UserId == Guid.Empty) {
            return new VocabularyWriteResult(VocabularyWriteStatus.NotFound, null, null);
        }

        var error = Validate(request);
        if (error is not null) {
            return new VocabularyWriteResult(VocabularyWriteStatus.Invalid, null, error);
        }

        var word = request.Word.Trim();
        var language = request.Language.Trim();
        var row = await Live()
            .FirstOrDefaultAsync(entry => entry.Id == entryId, cancellationToken);

        // Same word+language already saved on a different live row: update that row instead of duplicating.
        var duplicate = row is null
            ? await Live()
                .FirstOrDefaultAsync(
                    entry => entry.Language == language && entry.Word.ToLower() == word.ToLower(),
                    cancellationToken)
            : null;
        row ??= duplicate;

        if (row is not null && !BookAnnotationWritePolicy.ShouldApply(row.UpdatedAt, request.UpdatedAt)) {
            return new VocabularyWriteResult(VocabularyWriteStatus.Superseded, await ToItemAsync(row, cancellationToken), null);
        }

        var now = clock.GetUtcNow();
        if (row is null) {
            row = new VocabularyEntryRow { Id = entryId, UserId = currentUser.UserId, CreatedAt = now };
            db.VocabularyEntries.Add(row);
        }

        row.Word = word;
        row.Language = language;
        row.Definition = string.IsNullOrWhiteSpace(request.Definition) ? null : request.Definition.Trim();
        row.BookId = request.BookId;
        row.SourceLocator = string.IsNullOrWhiteSpace(request.SourceLocator) ? null : request.SourceLocator.Trim();
        row.SourceSentence = Clip(request.SourceSentence);
        row.Status = request.Status.DecodeAs<VocabularyStatus>();
        row.UpdatedAt = request.UpdatedAt;
        row.DeletedAt = null;
        await db.SaveChangesAsync(cancellationToken);
        return new VocabularyWriteResult(VocabularyWriteStatus.Applied, await ToItemAsync(row, cancellationToken), null);
    }

    /// <inheritdoc />
    public async Task<VocabularyWriteResult> DeleteAsync(
        Guid entryId,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken) {
        var row = await Live().FirstOrDefaultAsync(entry => entry.Id == entryId, cancellationToken);
        if (row is null) {
            return new VocabularyWriteResult(VocabularyWriteStatus.NotFound, null, null);
        }

        if (!BookAnnotationWritePolicy.IsPlausibleClock(updatedAt, clock.GetUtcNow())) {
            return new VocabularyWriteResult(VocabularyWriteStatus.Invalid, null, "updatedAt is too far in the future.");
        }

        if (!BookAnnotationWritePolicy.ShouldApply(row.UpdatedAt, updatedAt)) {
            return new VocabularyWriteResult(VocabularyWriteStatus.Superseded, await ToItemAsync(row, cancellationToken), null);
        }

        row.UpdatedAt = updatedAt;
        row.DeletedAt = updatedAt;
        await db.SaveChangesAsync(cancellationToken);
        return new VocabularyWriteResult(VocabularyWriteStatus.Applied, await ToItemAsync(row, cancellationToken), null);
    }

    /// <inheritdoc />
    public async Task<VocabularyExportFile> ExportAsync(
        AnnotationExportFormat format,
        VocabularyQuery query,
        CancellationToken cancellationToken) {
        var page = await ListAsync(query with { Limit = MaximumPageSize }, cancellationToken);
        return VocabularyExportWriter.Write(format, page.Items, clock.GetUtcNow());
    }

    private IQueryable<VocabularyEntryRow> Live() =>
        db.VocabularyEntries.Where(row => row.UserId == currentUser.UserId && row.DeletedAt == null);

    private static string? Validate(UpsertVocabularyEntryRequest request) {
        if (string.IsNullOrWhiteSpace(request.Word)) {
            return "word must not be empty.";
        }

        if (request.Word.Trim().Length > WordMaxLength) {
            return $"word must be at most {WordMaxLength} characters.";
        }

        if (string.IsNullOrWhiteSpace(request.Language)) {
            return "language must not be empty.";
        }

        if (request.Language.Trim().Length > LanguageMaxLength) {
            return $"language must be at most {LanguageMaxLength} characters.";
        }

        if (!request.Status.TryDecodeAs<VocabularyStatus>(out _)) {
            return $"Unknown status '{request.Status}'.";
        }

        return null;
    }

    private static string? Clip(string? value) =>
        string.IsNullOrEmpty(value) ? null : value.Length > SentenceMaxLength ? value[..SentenceMaxLength] : value;

    private async Task<VocabularyEntry> ToItemAsync(VocabularyEntryRow row, CancellationToken cancellationToken) {
        string? title = null;
        if (row.BookId is { } bookId) {
            title = await db.Entities.AsNoTracking()
                .Where(entity => entity.Id == bookId)
                .Select(entity => entity.Title)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new VocabularyEntry(
            row.Id,
            row.Word,
            row.Language,
            row.Definition,
            row.BookId,
            title,
            row.SourceLocator,
            row.SourceSentence,
            row.Status.ToCode(),
            row.CreatedAt,
            row.UpdatedAt);
    }

    private async Task<IReadOnlyList<VocabularyEntry>> ToItemsAsync(
        IReadOnlyList<VocabularyEntryRow> rows,
        CancellationToken cancellationToken) {
        var bookIds = rows.Where(row => row.BookId is not null).Select(row => row.BookId!.Value).Distinct().ToList();
        var titles = await db.Entities.AsNoTracking()
            .Where(entity => bookIds.Contains(entity.Id))
            .ToDictionaryAsync(entity => entity.Id, entity => entity.Title, cancellationToken);
        return rows.Select(row => new VocabularyEntry(
            row.Id,
            row.Word,
            row.Language,
            row.Definition,
            row.BookId,
            row.BookId is { } bookId ? titles.GetValueOrDefault(bookId) : null,
            row.SourceLocator,
            row.SourceSentence,
            row.Status.ToCode(),
            row.CreatedAt,
            row.UpdatedAt)).ToList();
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

using Microsoft.EntityFrameworkCore;
using Prismedia.Application.Books;
using Prismedia.Application.Reader;
using Prismedia.Application.Security;
using Prismedia.Contracts.Reader;
using Prismedia.Domain.Entities;
using Prismedia.Infrastructure.Persistence;
using Prismedia.Infrastructure.Persistence.Entities;

namespace Prismedia.Infrastructure.Media.Reader;

/// <summary>
/// EF store for the current user's reader preferences: one row per user, last-write-wins on
/// the client-supplied <c>updatedAt</c>, defaults returned when nothing is stored.
/// </summary>
internal sealed class EfReaderPreferencesService(
    PrismediaDbContext db,
    ICurrentUserContext currentUser) : IReaderPreferencesService {
    /// <summary>Documented defaults, matching the in-reader font-size control's existing bounds.</summary>
    public static readonly ReaderPreferencesResponse Defaults = new(
        ReaderFontFamily.Publisher.ToCode(),
        100,
        1.5,
        1,
        900,
        ReaderTheme.Auto.ToCode(),
        ReaderMode.Paged.ToCode(),
        null,
        null);

    private const int MinFontSizePercent = 80;
    private const int MaxFontSizePercent = 200;
    private const double MinLineHeight = 1.2;
    private const double MaxLineHeight = 2.0;
    private const int MinMarginStep = 0;
    private const int MaxMarginStep = 4;
    private const int MinContentWidth = 320;
    private const int MaxContentWidth = 1200;
    private const int MaxLanguageLength = 16;

    /// <inheritdoc />
    public async Task<ReaderPreferencesResponse> GetAsync(CancellationToken cancellationToken) {
        if (currentUser.UserId == Guid.Empty) {
            return Defaults;
        }

        var row = await db.UserReaderPreferences.AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.UserId == currentUser.UserId, cancellationToken);
        return row is null ? Defaults : ToContract(row);
    }

    /// <inheritdoc />
    public async Task<ReaderPreferencesWriteResult> UpsertAsync(
        UpsertReaderPreferencesRequest request,
        CancellationToken cancellationToken) {
        if (currentUser.UserId == Guid.Empty) {
            return new ReaderPreferencesWriteResult(ReaderPreferencesWriteStatus.Invalid, null, "No authenticated user.");
        }

        var error = Validate(request);
        if (error is not null) {
            return new ReaderPreferencesWriteResult(ReaderPreferencesWriteStatus.Invalid, null, error);
        }

        var row = await db.UserReaderPreferences
            .FirstOrDefaultAsync(entry => entry.UserId == currentUser.UserId, cancellationToken);
        if (row is not null && !BookAnnotationWritePolicy.ShouldApply(row.UpdatedAt, request.UpdatedAt)) {
            return new ReaderPreferencesWriteResult(ReaderPreferencesWriteStatus.Superseded, ToContract(row), null);
        }

        if (row is null) {
            row = new UserReaderPreferencesRow { UserId = currentUser.UserId };
            db.UserReaderPreferences.Add(row);
        }

        row.FontFamily = request.FontFamily.DecodeAs<ReaderFontFamily>();
        row.FontSizePercent = request.FontSizePercent;
        row.LineHeight = request.LineHeight;
        row.MarginStep = request.MarginStep;
        row.MaxContentWidth = request.MaxContentWidth;
        row.Theme = request.Theme.DecodeAs<ReaderTheme>();
        row.Flow = request.Flow.DecodeAs<ReaderMode>();
        row.TranslationTargetLanguage = string.IsNullOrWhiteSpace(request.TranslationTargetLanguage)
            ? null
            : request.TranslationTargetLanguage.Trim();
        row.UpdatedAt = request.UpdatedAt;
        await db.SaveChangesAsync(cancellationToken);
        return new ReaderPreferencesWriteResult(ReaderPreferencesWriteStatus.Applied, ToContract(row), null);
    }

    private static string? Validate(UpsertReaderPreferencesRequest request) {
        if (!request.FontFamily.TryDecodeAs<ReaderFontFamily>(out _)) {
            return $"Unknown font family '{request.FontFamily}'.";
        }

        if (!request.Theme.TryDecodeAs<ReaderTheme>(out _)) {
            return $"Unknown theme '{request.Theme}'.";
        }

        if (!request.Flow.TryDecodeAs<ReaderMode>(out var flow) || flow == ReaderMode.Webtoon) {
            return $"Unknown or unsupported reader flow '{request.Flow}'.";
        }

        if (request.FontSizePercent is < MinFontSizePercent or > MaxFontSizePercent) {
            return $"fontSizePercent must be between {MinFontSizePercent} and {MaxFontSizePercent}.";
        }

        if (request.LineHeight is < MinLineHeight or > MaxLineHeight) {
            return $"lineHeight must be between {MinLineHeight} and {MaxLineHeight}.";
        }

        if (request.MarginStep is < MinMarginStep or > MaxMarginStep) {
            return $"marginStep must be between {MinMarginStep} and {MaxMarginStep}.";
        }

        if (request.MaxContentWidth is < MinContentWidth or > MaxContentWidth) {
            return $"maxContentWidth must be between {MinContentWidth} and {MaxContentWidth}.";
        }

        if (request.TranslationTargetLanguage is { Length: > MaxLanguageLength }) {
            return $"translationTargetLanguage must be at most {MaxLanguageLength} characters.";
        }

        return null;
    }

    private static ReaderPreferencesResponse ToContract(UserReaderPreferencesRow row) => new(
        row.FontFamily.ToCode(),
        row.FontSizePercent,
        row.LineHeight,
        row.MarginStep,
        row.MaxContentWidth,
        row.Theme.ToCode(),
        row.Flow.ToCode(),
        row.TranslationTargetLanguage,
        row.UpdatedAt);
}

namespace Prismedia.Contracts.Books;

/// <summary>A user's bookmark on a page of a Book's readable file (separate from highlights).</summary>
/// <param name="Id">Client-generated stable identifier.</param>
/// <param name="BookId">Book Entity the bookmark belongs to.</param>
/// <param name="LocatorKind">Locator format code (<c>epub-cfi</c>).</param>
/// <param name="Locator">Format-specific locator; for EPUB the CFI at the start of the bookmarked page.</param>
/// <param name="SpineIndex">Reading-order section index, used for ordering.</param>
/// <param name="Position">Fraction 0..1 through the book, used for ordering and display.</param>
/// <param name="ChapterLabel">Chapter title at the bookmark, when known.</param>
/// <param name="Label">Optional user label.</param>
/// <param name="CreatedAt">When the bookmark was first stored.</param>
/// <param name="UpdatedAt">Last-write-wins clock supplied by the writing client.</param>
/// <param name="DeletedAt">Tombstone time; non-null means deleted.</param>
public sealed record BookBookmark(
    Guid Id,
    Guid BookId,
    string LocatorKind,
    string Locator,
    int SpineIndex,
    double Position,
    string? ChapterLabel,
    string? Label,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DeletedAt);

/// <summary>All of the current user's bookmarks for one Book, tombstones included.</summary>
/// <param name="Bookmarks">Bookmarks in reading order.</param>
public sealed record BookBookmarksResponse(IReadOnlyList<BookBookmark> Bookmarks);

/// <summary>Create-or-update payload for one bookmark.</summary>
/// <param name="LocatorKind">Locator format code.</param>
/// <param name="Locator">Format-specific locator.</param>
/// <param name="SpineIndex">Reading-order section index.</param>
/// <param name="Position">Fraction 0..1 through the book.</param>
/// <param name="ChapterLabel">Chapter title at the bookmark.</param>
/// <param name="Label">Optional user label; blank clears it.</param>
/// <param name="UpdatedAt">Client clock for last-write-wins.</param>
public sealed record UpsertBookBookmarkRequest(
    string LocatorKind,
    string Locator,
    int SpineIndex,
    double Position,
    string? ChapterLabel,
    string? Label,
    DateTimeOffset UpdatedAt);

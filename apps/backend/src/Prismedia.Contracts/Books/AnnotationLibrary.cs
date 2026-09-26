namespace Prismedia.Contracts.Books;

/// <summary>One highlight in the cross-library annotation list.</summary>
/// <param name="Id">Annotation identifier.</param>
/// <param name="BookId">Book Entity the highlight belongs to.</param>
/// <param name="BookTitle">Title of that Book.</param>
/// <param name="LocatorKind">Locator format code.</param>
/// <param name="Locator">Locator used to open the reader at the highlight.</param>
/// <param name="SpineIndex">Reading-order section index.</param>
/// <param name="Position">Fraction 0..1 through the book.</param>
/// <param name="SelectedText">The highlighted text.</param>
/// <param name="Note">Optional note.</param>
/// <param name="Style">Annotation style code.</param>
/// <param name="Color">Annotation colour code.</param>
/// <param name="IsStale">True when the book's file changed since the highlight was made.</param>
/// <param name="CreatedAt">When the highlight was created.</param>
/// <param name="UpdatedAt">When it was last changed.</param>
public sealed record AnnotationLibraryItem(
    Guid Id,
    Guid BookId,
    string BookTitle,
    string LocatorKind,
    string Locator,
    int SpineIndex,
    double Position,
    string SelectedText,
    string? Note,
    string Style,
    string Color,
    bool IsStale,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>A page of the cross-library annotation list, ordered by book then reading position.</summary>
/// <param name="Items">Highlights on this page.</param>
/// <param name="NextCursor">Opaque cursor for the next page, or null at the end.</param>
/// <param name="TotalCount">Total matching highlights across all pages.</param>
public sealed record AnnotationLibraryResponse(
    IReadOnlyList<AnnotationLibraryItem> Items,
    string? NextCursor,
    int TotalCount);

/// <summary>Per-book annotation counts for the dashboard.</summary>
/// <param name="BookId">Book Entity identifier.</param>
/// <param name="Title">Book title.</param>
/// <param name="HighlightCount">Live highlights in the book.</param>
/// <param name="NoteCount">Highlights that carry a note.</param>
/// <param name="StaleCount">Highlights made against a previous version of the book's file.</param>
/// <param name="LastAnnotatedAt">Most recent change among the book's highlights.</param>
public sealed record AnnotationLibraryBook(
    Guid BookId,
    string Title,
    int HighlightCount,
    int NoteCount,
    int StaleCount,
    DateTimeOffset LastAnnotatedAt);

/// <summary>Every book the current user has highlighted, with counts.</summary>
/// <param name="Books">Books ordered by title.</param>
public sealed record AnnotationLibraryBooksResponse(IReadOnlyList<AnnotationLibraryBook> Books);

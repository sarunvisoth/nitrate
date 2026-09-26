namespace Prismedia.Contracts.Books;

/// <summary>A user's highlight (optionally with a note) anchored to a range in a Book's readable file.</summary>
/// <param name="Id">Client-generated stable identifier.</param>
/// <param name="BookId">Book Entity the annotation belongs to.</param>
/// <param name="LocatorKind">Locator format code (<c>epub-cfi</c>).</param>
/// <param name="Locator">Format-specific range locator; for EPUB a range CFI.</param>
/// <param name="SpineIndex">Reading-order section index, used for ordering.</param>
/// <param name="Position">Fraction 0..1 through the book at the start of the range, used for ordering.</param>
/// <param name="SelectedText">The exact highlighted text (text-quote fallback for re-anchoring).</param>
/// <param name="TextBefore">Up to 256 characters immediately before the selection.</param>
/// <param name="TextAfter">Up to 256 characters immediately after the selection.</param>
/// <param name="Style">Annotation style code.</param>
/// <param name="Color">Annotation colour code.</param>
/// <param name="Note">Optional user note.</param>
/// <param name="SourceSignature">
/// Readable-file signature when the annotation was first saved; a later mismatch means the file changed.
/// </param>
/// <param name="CreatedAt">When the annotation was first stored.</param>
/// <param name="UpdatedAt">Last-write-wins clock supplied by the writing client.</param>
/// <param name="DeletedAt">Tombstone time; non-null means deleted.</param>
public sealed record BookAnnotation(
    Guid Id,
    Guid BookId,
    string LocatorKind,
    string Locator,
    int SpineIndex,
    double Position,
    string SelectedText,
    string? TextBefore,
    string? TextAfter,
    string Style,
    string Color,
    string? Note,
    string? SourceSignature,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DeletedAt);

/// <summary>All of the current user's annotations for one Book, tombstones included.</summary>
/// <param name="Annotations">Annotations in reading order.</param>
public sealed record BookAnnotationsResponse(IReadOnlyList<BookAnnotation> Annotations);

/// <summary>Create-or-update payload for one annotation.</summary>
/// <param name="LocatorKind">Locator format code.</param>
/// <param name="Locator">Format-specific range locator.</param>
/// <param name="SpineIndex">Reading-order section index.</param>
/// <param name="Position">Fraction 0..1 through the book at the start of the range.</param>
/// <param name="SelectedText">The exact highlighted text.</param>
/// <param name="TextBefore">Context immediately before the selection.</param>
/// <param name="TextAfter">Context immediately after the selection.</param>
/// <param name="Style">Annotation style code.</param>
/// <param name="Color">Annotation colour code.</param>
/// <param name="Note">Optional user note; blank clears it.</param>
/// <param name="UpdatedAt">Client clock for last-write-wins.</param>
public sealed record UpsertBookAnnotationRequest(
    string LocatorKind,
    string Locator,
    int SpineIndex,
    double Position,
    string SelectedText,
    string? TextBefore,
    string? TextAfter,
    string Style,
    string Color,
    string? Note,
    DateTimeOffset UpdatedAt);

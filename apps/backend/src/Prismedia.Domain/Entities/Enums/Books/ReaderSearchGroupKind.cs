namespace Prismedia.Domain.Entities;

/// <summary>
/// Closed set of unified-search result groups owned by the reader domain (as opposed to the
/// Entity-kind groups already covered by <c>EntityKindRegistry</c>). Frontend clients reference
/// these codes from the generated <c>codes.ts</c> instead of retyping the literal strings.
/// </summary>
public enum ReaderSearchGroupKind {
    /// <summary>Matches against the current user's saved highlights and notes.</summary>
    [Code("annotation")]
    Annotation,

    /// <summary>Matches against the current user's saved vocabulary words and definitions.</summary>
    [Code("vocabulary")]
    Vocabulary
}

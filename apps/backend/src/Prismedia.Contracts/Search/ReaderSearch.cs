using Prismedia.Contracts.Books;
using Prismedia.Contracts.Vocabulary;

namespace Prismedia.Contracts.Search;

/// <summary>
/// Unified reader-domain search results for the current user: highlights/notes and saved
/// vocabulary words matching a query, alongside the library Entity search the frontend already
/// performs against <c>/api/entities</c>. Additive — does not change any existing endpoint.
/// </summary>
/// <param name="Annotations">Matching live highlights, most relevant first, capped to the request limit.</param>
/// <param name="Vocabulary">Matching saved vocabulary words, capped to the request limit.</param>
public sealed record ReaderSearchResponse(
    IReadOnlyList<AnnotationLibraryItem> Annotations,
    IReadOnlyList<VocabularyEntry> Vocabulary);

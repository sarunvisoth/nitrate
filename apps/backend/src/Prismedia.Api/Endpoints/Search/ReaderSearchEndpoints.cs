using Prismedia.Application.Books;
using Prismedia.Application.Vocabulary;
using Prismedia.Contracts.Search;

namespace Prismedia.Api.Endpoints;

/// <summary>
/// Unified search over the reader domain: the current user's highlights/notes and saved
/// vocabulary. Additive to the existing library-Entity search the frontend already performs
/// against <c>/api/entities</c> — the closed-source apps do not call this endpoint, so it carries
/// no compatibility constraint of its own.
/// </summary>
public static class ReaderSearchEndpoints {
    private const int DefaultLimit = 5;
    private const int MaximumLimit = 50;
    private const int MinimumQueryLength = 2;

    public static RouteGroupBuilder MapReaderSearchEndpoints(this IEndpointRouteBuilder routes) {
        var group = routes.MapGroup("/api/search").WithTags("Search");

        group.MapGet("/reader", async (
            string? q,
            int? limit,
            IAnnotationLibraryService annotations,
            IVocabularyService vocabulary,
            CancellationToken cancellationToken) => {
                var term = q?.Trim();
                if (string.IsNullOrEmpty(term) || term.Length < MinimumQueryLength) {
                    return Results.Ok(new ReaderSearchResponse([], []));
                }

                var pageSize = Math.Clamp(limit ?? DefaultLimit, 1, MaximumLimit);
                var annotationResults = await annotations.ListAsync(
                    new AnnotationLibraryQuery(term, null, null, false, null, pageSize),
                    cancellationToken);
                var vocabularyResults = await vocabulary.ListAsync(
                    new VocabularyQuery(term, null, null, null, pageSize),
                    cancellationToken);
                return Results.Ok(new ReaderSearchResponse(annotationResults.Items, vocabularyResults.Items));
            })
            .WithName("SearchReader")
            .WithSummary("Search the current user's highlights, notes, and saved vocabulary words.")
            .Produces<ReaderSearchResponse>();

        return group;
    }
}

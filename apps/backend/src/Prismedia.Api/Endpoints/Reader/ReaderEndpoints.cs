using Prismedia.Application.Reader;
using Prismedia.Contracts.Reader;
using Prismedia.Contracts.System;

namespace Prismedia.Api.Endpoints;

/// <summary>
/// The reader's Define/Translate/Wikipedia lookup endpoints. Every route proxies one external
/// provider through <see cref="ReaderLookupService"/> so the browser never calls a third party
/// directly; auth is enforced by the shared <c>/api/*</c> authentication middleware, same as the
/// rest of the Books surface.
/// </summary>
public static class ReaderEndpoints {
    public static RouteGroupBuilder MapReaderEndpoints(this IEndpointRouteBuilder routes) {
        var group = routes.MapGroup("/api/reader")
            .WithTags("Reader Lookups");

        group.MapGet("/define", async (
            string? term,
            string? language,
            ReaderLookupService lookups,
            CancellationToken cancellationToken) =>
                ToResult(await lookups.DefineAsync(term, language, cancellationToken)))
            .WithName("DefineReaderTerm")
            .WithSummary("Look up a dictionary definition for a selected word.")
            .Produces<DictionaryDefinitionResponse>()
            .Produces<ApiProblem>(StatusCodes.Status400BadRequest)
            .Produces<ApiProblem>(StatusCodes.Status404NotFound)
            .Produces<ApiProblem>(StatusCodes.Status502BadGateway);

        group.MapPost("/translate", async (
            TranslateTextRequest request,
            ReaderLookupService lookups,
            CancellationToken cancellationToken) =>
                ToResult(await lookups.TranslateAsync(request, cancellationToken)))
            .WithName("TranslateReaderText")
            .WithSummary("Translate selected text.")
            .Produces<TranslateTextResponse>()
            .Produces<ApiProblem>(StatusCodes.Status400BadRequest)
            .Produces<ApiProblem>(StatusCodes.Status502BadGateway);

        group.MapGet("/wikipedia", async (
            string? term,
            string? language,
            ReaderLookupService lookups,
            CancellationToken cancellationToken) =>
                ToResult(await lookups.WikipediaAsync(term, language, cancellationToken)))
            .WithName("GetReaderWikipediaSummary")
            .WithSummary("Look up a Wikipedia summary for a selected term.")
            .Produces<WikipediaSummaryResponse>()
            .Produces<ApiProblem>(StatusCodes.Status400BadRequest)
            .Produces<ApiProblem>(StatusCodes.Status404NotFound)
            .Produces<ApiProblem>(StatusCodes.Status502BadGateway);

        return group;
    }

    private static IResult ToResult<T>(ReaderLookupOutcome<T> outcome) =>
        outcome.Status switch {
            ReaderLookupStatus.Ok => Results.Ok(outcome.Value),
            ReaderLookupStatus.Invalid => Results.BadRequest(
                new ApiProblem(ApiProblemCodes.ReaderLookupInvalid, outcome.Error ?? "The request is invalid.")),
            ReaderLookupStatus.NotFound => Results.NotFound(
                new ApiProblem(ApiProblemCodes.EntityNotFound, "No result was found.")),
            ReaderLookupStatus.ProviderUnavailable => Results.Json(
                new ApiProblem(
                    ApiProblemCodes.ReaderLookupProviderUnavailable,
                    outcome.Error ?? "The lookup provider is unavailable."),
                statusCode: StatusCodes.Status502BadGateway),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome))
        };
}

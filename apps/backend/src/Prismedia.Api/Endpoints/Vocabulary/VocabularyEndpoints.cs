using Prismedia.Application.Vocabulary;
using Prismedia.Contracts.System;
using Prismedia.Contracts.Vocabulary;
using Prismedia.Domain.Entities;

namespace Prismedia.Api.Endpoints;

/// <summary>The current user's saved vocabulary words: search, status changes, delete, export.</summary>
public static class VocabularyEndpoints {
    private const int DefaultPageSize = 200;

    public static RouteGroupBuilder MapVocabularyEndpoints(this IEndpointRouteBuilder routes) {
        var group = routes.MapGroup("/api/vocabulary").WithTags("Vocabulary");

        group.MapGet("/", async (
            string? search,
            string? status,
            Guid? bookId,
            string? cursor,
            int? limit,
            IVocabularyService vocabulary,
            CancellationToken cancellationToken) => {
                VocabularyStatus? statusFilter = null;
                if (!string.IsNullOrWhiteSpace(status)) {
                    if (!status.Trim().TryDecodeAs<VocabularyStatus>(out var decoded)) {
                        return Results.BadRequest(new ApiProblem(ApiProblemCodes.RequestInvalid, $"Unknown status '{status}'."));
                    }

                    statusFilter = decoded;
                }

                return Results.Ok(await vocabulary.ListAsync(
                    new VocabularyQuery(search, statusFilter, bookId, cursor, limit ?? DefaultPageSize),
                    cancellationToken));
            })
            .WithName("ListVocabulary")
            .WithSummary("Search the current user's saved vocabulary words.")
            .Produces<VocabularyListResponse>()
            .Produces<ApiProblem>(StatusCodes.Status400BadRequest);

        group.MapPut("/{entryId:guid}", async (
            Guid entryId,
            UpsertVocabularyEntryRequest request,
            IVocabularyService vocabulary,
            CancellationToken cancellationToken) => {
                var result = await vocabulary.UpsertAsync(entryId, request, cancellationToken);
                return result.Status switch {
                    VocabularyWriteStatus.Applied or VocabularyWriteStatus.Superseded => Results.Ok(result.Entry),
                    VocabularyWriteStatus.NotFound => Results.NotFound(
                        new ApiProblem(ApiProblemCodes.VocabularyNotFound, "No authenticated user.")),
                    VocabularyWriteStatus.Invalid => Results.BadRequest(
                        new ApiProblem(ApiProblemCodes.VocabularyInvalid, result.Error ?? "Invalid vocabulary entry.")),
                    _ => throw new ArgumentOutOfRangeException(nameof(result))
                };
            })
            .WithName("PutVocabularyEntry")
            .WithSummary("Create or update one saved word (last-write-wins); a duplicate word+language updates the existing entry.")
            .Produces<VocabularyEntry>()
            .Produces<ApiProblem>(StatusCodes.Status400BadRequest)
            .Produces<ApiProblem>(StatusCodes.Status404NotFound);

        group.MapDelete("/{entryId:guid}", async (
            Guid entryId,
            DateTimeOffset updatedAt,
            IVocabularyService vocabulary,
            CancellationToken cancellationToken) => {
                var result = await vocabulary.DeleteAsync(entryId, updatedAt, cancellationToken);
                return result.Status switch {
                    VocabularyWriteStatus.Applied or VocabularyWriteStatus.Superseded => Results.Ok(result.Entry),
                    VocabularyWriteStatus.NotFound => Results.NotFound(
                        new ApiProblem(ApiProblemCodes.VocabularyNotFound, "Vocabulary entry not found.")),
                    VocabularyWriteStatus.Invalid => Results.BadRequest(
                        new ApiProblem(ApiProblemCodes.VocabularyInvalid, result.Error ?? "Invalid delete.")),
                    _ => throw new ArgumentOutOfRangeException(nameof(result))
                };
            })
            .WithName("DeleteVocabularyEntry")
            .WithSummary("Tombstone one saved word (last-write-wins).")
            .Produces<VocabularyEntry>()
            .Produces<ApiProblem>(StatusCodes.Status400BadRequest)
            .Produces<ApiProblem>(StatusCodes.Status404NotFound);

        group.MapGet("/export", async (
            string format,
            string? search,
            string? status,
            Guid? bookId,
            IVocabularyService vocabulary,
            CancellationToken cancellationToken) => {
                if (!format.Trim().TryDecodeAs<AnnotationExportFormat>(out var exportFormat)) {
                    return Results.BadRequest(new ApiProblem(ApiProblemCodes.RequestInvalid, $"Unknown export format '{format}'."));
                }

                VocabularyStatus? statusFilter = null;
                if (!string.IsNullOrWhiteSpace(status) && status.Trim().TryDecodeAs<VocabularyStatus>(out var decoded)) {
                    statusFilter = decoded;
                }

                var file = await vocabulary.ExportAsync(
                    exportFormat,
                    new VocabularyQuery(search, statusFilter, bookId, null, DefaultPageSize),
                    cancellationToken);
                return Results.File(file.Content, file.ContentType, fileDownloadName: file.FileName);
            })
            .WithName("ExportVocabulary")
            .WithSummary("Download the current user's saved vocabulary as Markdown, JSON, or CSV.")
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .Produces<ApiProblem>(StatusCodes.Status400BadRequest);

        return group;
    }
}

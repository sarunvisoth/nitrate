using Prismedia.Application.Books;
using Prismedia.Contracts.Books;
using Prismedia.Contracts.Entities;
using Prismedia.Contracts.System;
using Prismedia.Domain.Entities;

namespace Prismedia.Api.Endpoints;

public static class BookEndpoints {
    public static RouteGroupBuilder MapBookEndpoints(this IEndpointRouteBuilder routes) {
        var group = routes.MapEntityKindRoutes(
            "/api/books",
            EntityKind.Book.ToCode(),
            "Books",
            "ListBooks",
            "GetBook");

        group.MapGet("/{id:guid}/contents", async (
            Guid id,
            IBookContentsService contents,
            CancellationToken cancellationToken) => {
                var result = await contents.GetAsync(id, cancellationToken);
                return result is null
                    ? Results.NotFound(new ApiProblem(
                        ApiProblemCodes.EntityNotFound,
                        $"Readable EPUB contents for book '{id}' were not found."))
                    : Results.Ok(result);
            })
            .WithName("GetBookContents")
            .WithSummary("Get compact EPUB contents and reading-order ranges.")
            .Produces<BookContentsResponse>()
            .Produces<ApiProblem>(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/chapter-mappings", async (
            Guid id,
            IBookChapterMappingService mappings,
            CancellationToken cancellationToken) => {
                var result = await mappings.GetAsync(id, cancellationToken);
                return result is null
                    ? Results.NotFound(new ApiProblem(
                        ApiProblemCodes.EntityNotFound,
                        $"Book '{id}' was not found."))
                    : Results.Ok(result);
            })
            .WithName("GetBookChapterMappings")
            .WithSummary("Get the Book's explicit audiobook-to-readable-chapter map.")
            .Produces<BookChapterMappingsResponse>()
            .Produces<ApiProblem>(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/chapter-mappings", async (
            Guid id,
            ReplaceBookChapterMappingsRequest request,
            IBookChapterMappingService mappings,
            CancellationToken cancellationToken) => {
                var result = await mappings.ReplaceAsync(id, request, cancellationToken);
                return result.Status switch {
                    BookChapterMappingSaveStatus.Saved => Results.Ok(result.Response),
                    BookChapterMappingSaveStatus.NotFound => Results.NotFound(new ApiProblem(
                        ApiProblemCodes.EntityNotFound,
                        $"Book '{id}' was not found.")),
                    BookChapterMappingSaveStatus.Invalid => Results.BadRequest(new ApiProblem(
                        ApiProblemCodes.InvalidBookChapterMapping,
                        result.Error ?? "The chapter mapping is invalid.")),
                    _ => throw new ArgumentOutOfRangeException(nameof(result.Status))
                };
            })
            .WithName("ReplaceBookChapterMappings")
            .WithSummary("Replace the Book's explicit audiobook-to-readable-chapter map.")
            .Produces<BookChapterMappingsResponse>()
            .Produces<ApiProblem>(StatusCodes.Status400BadRequest)
            .Produces<ApiProblem>(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/annotations", async (
            Guid id,
            IBookAnnotationService annotations,
            CancellationToken cancellationToken) => {
                var result = await annotations.ListAsync(id, cancellationToken);
                return result is null
                    ? Results.NotFound(new ApiProblem(
                        ApiProblemCodes.EntityNotFound,
                        $"Book '{id}' was not found."))
                    : Results.Ok(result);
            })
            .WithName("ListBookAnnotations")
            .WithSummary("List the current user's highlights and notes for a Book, including deleted tombstones.")
            .Produces<BookAnnotationsResponse>()
            .Produces<ApiProblem>(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/annotations/{annotationId:guid}", async (
            Guid id,
            Guid annotationId,
            UpsertBookAnnotationRequest request,
            IBookAnnotationService annotations,
            CancellationToken cancellationToken) =>
                ToAnnotationResult(id, await annotations.UpsertAsync(id, annotationId, request, cancellationToken)))
            .WithName("UpsertBookAnnotation")
            .WithSummary("Create or update one annotation; the write with the newest updatedAt wins.")
            .Produces<BookAnnotation>()
            .Produces<ApiProblem>(StatusCodes.Status400BadRequest)
            .Produces<ApiProblem>(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}/annotations/{annotationId:guid}", async (
            Guid id,
            Guid annotationId,
            DateTimeOffset updatedAt,
            IBookAnnotationService annotations,
            CancellationToken cancellationToken) =>
                ToAnnotationResult(id, await annotations.DeleteAsync(id, annotationId, updatedAt, cancellationToken)))
            .WithName("DeleteBookAnnotation")
            .WithSummary("Tombstone one annotation; the write with the newest updatedAt wins.")
            .Produces<BookAnnotation>()
            .Produces<ApiProblem>(StatusCodes.Status400BadRequest)
            .Produces<ApiProblem>(StatusCodes.Status404NotFound);

        return group;
    }

    private static IResult ToAnnotationResult(Guid bookId, BookAnnotationWriteResult result) =>
        result.Status switch {
            BookAnnotationWriteStatus.Applied or BookAnnotationWriteStatus.Superseded => Results.Ok(result.Annotation),
            BookAnnotationWriteStatus.NotFound => Results.NotFound(new ApiProblem(
                ApiProblemCodes.EntityNotFound,
                $"Annotation on book '{bookId}' was not found.")),
            BookAnnotationWriteStatus.Invalid => Results.BadRequest(new ApiProblem(
                ApiProblemCodes.InvalidBookAnnotation,
                result.Error ?? "The annotation is invalid.")),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
}

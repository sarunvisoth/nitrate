using Prismedia.Application.Books;
using Prismedia.Contracts.Books;
using Prismedia.Contracts.System;
using Prismedia.Domain.Entities;

namespace Prismedia.Api.Endpoints;

/// <summary>Cross-library highlights and notes: the dashboard list, per-book counts, and export.</summary>
public static class AnnotationLibraryEndpoints {
    private const int DefaultPageSize = 200;

    public static RouteGroupBuilder MapAnnotationLibraryEndpoints(this IEndpointRouteBuilder routes) {
        var group = routes.MapGroup("/api/annotations").WithTags("Annotations");

        group.MapGet("/", async (
            string? search,
            Guid? bookId,
            string? color,
            bool? notesOnly,
            string? cursor,
            int? limit,
            IAnnotationLibraryService library,
            CancellationToken cancellationToken) => {
                AnnotationColor? colorFilter = null;
                if (!string.IsNullOrWhiteSpace(color)) {
                    if (!color.Trim().TryDecodeAs<AnnotationColor>(out var decoded)) {
                        return Results.BadRequest(new ApiProblem(ApiProblemCodes.RequestInvalid, $"Unknown color '{color}'."));
                    }

                    colorFilter = decoded;
                }

                return Results.Ok(await library.ListAsync(
                    new AnnotationLibraryQuery(search, bookId, colorFilter, notesOnly ?? false, cursor, limit ?? DefaultPageSize),
                    cancellationToken));
            })
            .WithName("ListAnnotationLibrary")
            .WithSummary("Search the current user's highlights and notes across every visible book.")
            .Produces<AnnotationLibraryResponse>()
            .Produces<ApiProblem>(StatusCodes.Status400BadRequest);

        group.MapGet("/books", async (
            IAnnotationLibraryService library,
            CancellationToken cancellationToken) => Results.Ok(await library.BooksAsync(cancellationToken)))
            .WithName("ListAnnotationLibraryBooks")
            .WithSummary("List every book the current user has highlighted, with highlight and note counts.")
            .Produces<AnnotationLibraryBooksResponse>();

        group.MapGet("/export", async (
            string format,
            Guid? bookId,
            IAnnotationLibraryService library,
            CancellationToken cancellationToken) => {
                if (!format.Trim().TryDecodeAs<AnnotationExportFormat>(out var exportFormat)) {
                    return Results.BadRequest(new ApiProblem(ApiProblemCodes.RequestInvalid, $"Unknown export format '{format}'."));
                }

                var file = await library.ExportAsync(exportFormat, bookId, cancellationToken);
                return Results.File(file.Content, file.ContentType, fileDownloadName: file.FileName);
            })
            .WithName("ExportAnnotationLibrary")
            .WithSummary("Download the current user's highlights and notes as Markdown, JSON, or CSV.")
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .Produces<ApiProblem>(StatusCodes.Status400BadRequest);

        return group;
    }
}

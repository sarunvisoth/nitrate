using Prismedia.Application.Entities;
using Prismedia.Contracts.System;

namespace Prismedia.Api.Endpoints;

internal static class EntityFileEndpoint {
    internal static RouteGroupBuilder MapEntityFileEndpoint(this RouteGroupBuilder group) {
        group.MapGet("/{id:guid}/files/{role}", StreamEntityFileAsync)
            .WithName("GetEntityFile")
            .WithSummary("Streams an entity-attached file by semantic role.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status206PartialContent)
            .Produces<ApiProblem>(StatusCodes.Status404NotFound);

        group.MapMethods("/{id:guid}/files/{role}", [HttpMethods.Head], StreamEntityFileAsync)
            .WithName("HeadEntityFile")
            .WithSummary("Probes an entity-attached file by semantic role.");

        return group;
    }

    private static async Task<IResult> StreamEntityFileAsync(
        Guid id,
        string role,
        IEntityFileContentService files,
        CancellationToken cancellationToken) {
        var content = await files.GetContentAsync(id, role, cancellationToken);
        if (content is null) {
            return Results.NotFound(new ApiProblem(ApiProblemCodes.EntityFileNotFound, $"Entity file '{role}' for '{id}' was not found."));
        }

        return await EntityFileResults.StreamAsync(
            content.Path,
            content.ContentType,
            () => Results.NotFound(new ApiProblem(ApiProblemCodes.EntityFileNotFound, $"Entity file '{role}' for '{id}' was not found.")),
            cancellationToken);
    }
}

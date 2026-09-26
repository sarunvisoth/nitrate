using Prismedia.Application.Plugins;
using Prismedia.Contracts.Plugins;
using Prismedia.Contracts.System;

namespace Prismedia.Api.Endpoints;

internal static class IdentifyBulkEndpoints {
    internal static RouteGroupBuilder MapIdentifyBulkEndpoints(this RouteGroupBuilder group) {
        group.MapPost("/bulk", async (
            IdentifyBulkStartRequest request,
            bool? hideNsfw,
            HttpContext httpContext,
            IIdentifyQueueService queue,
            CancellationToken cancellationToken) => {
                if (request.EntityIds.Count == 0) {
                    return Results.BadRequest(new ApiProblem(ApiProblemCodes.EmptyBulkIdentify, "Bulk identify requires at least one entity."));
                }

                var response = await queue.RequestSearchBatchAsync(
                    request.EntityIds,
                    new IdentifyQueueSearchRequest(request.Provider, request.Query),
                    NsfwVisibility.ShouldHide(hideNsfw, httpContext),
                    cancellationToken);

                return Results.Accepted("/api/identify/queue", response);
            })
            .WithName("StartBulkIdentify")
            .WithSummary("Requests identify searches for a batch of entities, one identify-search job per entity.")
            .Produces<IdentifyBulkAcceptedResponse>(StatusCodes.Status202Accepted)
            .Produces<ApiProblem>(StatusCodes.Status400BadRequest);

        return group;
    }
}

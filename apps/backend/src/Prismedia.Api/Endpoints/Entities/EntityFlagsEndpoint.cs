using Prismedia.Api.Security;
using Prismedia.Application.Entities;
using Prismedia.Contracts.Entities;
using Prismedia.Contracts.System;
using Prismedia.Domain.Entities;

namespace Prismedia.Api.Endpoints;

internal static class EntityFlagsEndpoint {
    internal static RouteGroupBuilder MapEntityFlagsEndpoint(this RouteGroupBuilder group) {
        group.MapPatch("/{id:guid}/flags", async (
            Guid id,
            EntityFlagsUpdateRequest request,
            HttpContext httpContext,
            EntityCapabilityService capabilities,
            CancellationToken cancellationToken) => {
            // Favorite is the caller's own opinion; NSFW and organized are library curation
            // facts and stay admin-only.
            if ((request.IsNsfw is not null || request.IsOrganized is not null) &&
                httpContext.GetCurrentUser() is not { Role: UserRole.Admin }) {
                return Results.Json(
                    new ApiProblem(ApiProblemCodes.AdminRequired, "Administrator access is required to change curation flags."),
                    statusCode: StatusCodes.Status403Forbidden);
            }

            return EntityEndpointResults.ToResult(id, await capabilities.UpdateFlagsAsync(
                id, request.IsFavorite, request.IsNsfw, request.IsOrganized, cancellationToken));
        })
            .WithName("UpdateEntityFlags")
            .WithSummary("Update Entity Flags.")
            .Produces<EntityCard>()
            .Produces<ApiProblem>(StatusCodes.Status403Forbidden)
            .Produces<ApiProblem>(StatusCodes.Status404NotFound);

        return group;
    }
}

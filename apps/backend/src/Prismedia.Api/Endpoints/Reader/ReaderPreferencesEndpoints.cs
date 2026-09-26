using Prismedia.Application.Reader;
using Prismedia.Contracts.Reader;
using Prismedia.Contracts.System;

namespace Prismedia.Api.Endpoints;

/// <summary>The current user's server-stored reader typography, theme, flow, and translation defaults.</summary>
public static class ReaderPreferencesEndpoints {
    public static RouteGroupBuilder MapReaderPreferencesEndpoints(this IEndpointRouteBuilder routes) {
        var group = routes.MapGroup("/api/reader/preferences").WithTags("Reader Preferences");

        group.MapGet("/", async (
            IReaderPreferencesService preferences,
            CancellationToken cancellationToken) => Results.Ok(await preferences.GetAsync(cancellationToken)))
            .WithName("GetReaderPreferences")
            .WithSummary("Get the current user's reader preferences, or defaults when none are saved.")
            .Produces<ReaderPreferencesResponse>();

        group.MapPut("/", async (
            UpsertReaderPreferencesRequest request,
            IReaderPreferencesService preferences,
            CancellationToken cancellationToken) => {
                var result = await preferences.UpsertAsync(request, cancellationToken);
                return result.Status switch {
                    ReaderPreferencesWriteStatus.Applied or ReaderPreferencesWriteStatus.Superseded =>
                        Results.Ok(result.Preferences),
                    ReaderPreferencesWriteStatus.Invalid => Results.BadRequest(
                        new ApiProblem(ApiProblemCodes.ReaderPreferencesInvalid, result.Error ?? "Invalid preferences.")),
                    _ => throw new ArgumentOutOfRangeException(nameof(result))
                };
            })
            .WithName("PutReaderPreferences")
            .WithSummary("Create or update the current user's reader preferences (last-write-wins).")
            .Produces<ReaderPreferencesResponse>()
            .Produces<ApiProblem>(StatusCodes.Status400BadRequest);

        return group;
    }
}

namespace Prismedia.Api.Codegen;

/// <summary>
/// Development-only endpoint that publishes the backend <see cref="CodesManifest"/> as
/// JSON for the frontend code generator. It is served under <c>/api/_codegen</c> (which the
/// dev proxy passes through and authentication treats as public) and is only mapped in the
/// Development environment, so it is never exposed in production images.
/// </summary>
public static class CodegenEndpoints {
    /// <summary>Route prefix treated as public by authentication.</summary>
    public const string CodegenPrefix = "/api/_codegen";

    /// <summary>Route the code-generation manifest is served from.</summary>
    public const string CodesManifestPath = "/api/_codegen/codes.json";

    /// <summary>Maps the code-generation manifest endpoint.</summary>
    public static IEndpointRouteBuilder MapPrismediaCodegen(this IEndpointRouteBuilder endpoints) {
        endpoints.MapGet(CodesManifestPath, () => Results.Json(CodesManifest.Build()));
        return endpoints;
    }
}

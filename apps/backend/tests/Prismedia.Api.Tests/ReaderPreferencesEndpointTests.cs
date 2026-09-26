using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Prismedia.Application.Reader;
using Prismedia.Contracts.Reader;
using Prismedia.Contracts.System;

namespace Prismedia.Api.Tests;

public sealed class ReaderPreferencesEndpointTests {
    private static readonly ReaderPreferencesResponse Defaults = new(
        "publisher", 100, 1.5, 1, 900, "auto", "paged", null, null);

    [Fact]
    public async Task GetReturnsTheServiceResponse() {
        using var factory = CreateFactory(new FakePreferences());
        using var client = factory.CreateAuthenticatedClient();

        var body = await client.GetFromJsonAsync<ReaderPreferencesResponse>("/api/reader/preferences");

        Assert.Equal(Defaults, body);
    }

    [Fact]
    public async Task PutSendsTheRequestAndReturnsTheResult() {
        var service = new FakePreferences();
        using var factory = CreateFactory(service);
        using var client = factory.CreateAuthenticatedClient();
        var request = new UpsertReaderPreferencesRequest(
            "serif", 130, 1.7, 2, 800, "sepia", "scrolled", "fr", DateTimeOffset.UnixEpoch);

        using var response = await client.PutAsJsonAsync("/api/reader/preferences", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(request, service.LastRequest);
    }

    [Fact]
    public async Task PutSurfacesValidationErrorsAsBadRequest() {
        using var factory = CreateFactory(new FakePreferences(invalid: true));
        using var client = factory.CreateAuthenticatedClient();

        using var response = await client.PutAsJsonAsync(
            "/api/reader/preferences",
            new UpsertReaderPreferencesRequest("publisher", 100, 1.5, 1, 900, "auto", "paged", null, DateTimeOffset.UnixEpoch));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            ApiProblemCodes.ReaderPreferencesInvalid,
            (await response.Content.ReadFromJsonAsync<ApiProblem>())!.Code);
    }

    private static WebApplicationFactory<Program> CreateFactory(IReaderPreferencesService service) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.ConfigureServices(services => {
                services.RemoveAll<IReaderPreferencesService>();
                services.AddSingleton(service);
            }))
            .WithTestAuth();

    private sealed class FakePreferences(bool invalid = false) : IReaderPreferencesService {
        public UpsertReaderPreferencesRequest? LastRequest { get; private set; }

        public Task<ReaderPreferencesResponse> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Defaults);

        public Task<ReaderPreferencesWriteResult> UpsertAsync(
            UpsertReaderPreferencesRequest request, CancellationToken cancellationToken) {
            LastRequest = request;
            return Task.FromResult(invalid
                ? new ReaderPreferencesWriteResult(ReaderPreferencesWriteStatus.Invalid, null, "bad")
                : new ReaderPreferencesWriteResult(ReaderPreferencesWriteStatus.Applied, Defaults, null));
        }
    }
}

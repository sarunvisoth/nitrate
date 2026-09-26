using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Prismedia.Application.Books;
using Prismedia.Contracts.Books;
using Prismedia.Contracts.System;
using Prismedia.Domain.Entities;

namespace Prismedia.Api.Tests;

public sealed class AnnotationLibraryEndpointTests {
    [Fact]
    public async Task ListBindsFiltersAndDecodesTheColour() {
        var service = new FakeLibrary();
        using var factory = CreateFactory(service);
        using var client = factory.CreateAuthenticatedClient();
        var bookId = Guid.NewGuid();

        using var response = await client.GetAsync(
            $"/api/annotations?search=noise&bookId={bookId}&color={AnnotationColor.Blue.ToCode()}&notesOnly=true&limit=25");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new AnnotationLibraryQuery("noise", bookId, AnnotationColor.Blue, true, null, 25), service.LastQuery);
    }

    [Fact]
    public async Task UnknownColourOrFormatIsABadRequest() {
        using var factory = CreateFactory(new FakeLibrary());
        using var client = factory.CreateAuthenticatedClient();

        using var badColor = await client.GetAsync("/api/annotations?color=orange");
        using var badFormat = await client.GetAsync("/api/annotations/export?format=docx");

        Assert.Equal(HttpStatusCode.BadRequest, badColor.StatusCode);
        Assert.Equal(ApiProblemCodes.RequestInvalid, (await badColor.Content.ReadFromJsonAsync<ApiProblem>())!.Code);
        Assert.Equal(HttpStatusCode.BadRequest, badFormat.StatusCode);
    }

    [Fact]
    public async Task ExportDownloadsAnAttachment() {
        using var factory = CreateFactory(new FakeLibrary());
        using var client = factory.CreateAuthenticatedClient();

        using var response = await client.GetAsync($"/api/annotations/export?format={AnnotationExportFormat.Csv.ToCode()}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("export.csv", response.Content.Headers.ContentDisposition.FileName?.Trim('"'));
        Assert.Equal("a,b\r\n", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task BooksListsCounts() {
        using var factory = CreateFactory(new FakeLibrary());
        using var client = factory.CreateAuthenticatedClient();

        var body = await client.GetFromJsonAsync<AnnotationLibraryBooksResponse>("/api/annotations/books");

        Assert.Equal("Dune", Assert.Single(body!.Books).Title);
    }

    private static WebApplicationFactory<Program> CreateFactory(IAnnotationLibraryService service) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.ConfigureServices(services => {
                services.RemoveAll<IAnnotationLibraryService>();
                services.AddSingleton(service);
            }))
            .WithTestAuth();

    private sealed class FakeLibrary : IAnnotationLibraryService {
        public AnnotationLibraryQuery? LastQuery { get; private set; }

        public Task<AnnotationLibraryResponse> ListAsync(AnnotationLibraryQuery query, CancellationToken cancellationToken) {
            LastQuery = query;
            return Task.FromResult(new AnnotationLibraryResponse([], null, 0));
        }

        public Task<AnnotationLibraryBooksResponse> BooksAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new AnnotationLibraryBooksResponse([
                new AnnotationLibraryBook(Guid.NewGuid(), "Dune", 19, 5, 0, DateTimeOffset.UnixEpoch)
            ]));

        public Task<AnnotationExportFile> ExportAsync(AnnotationExportFormat format, Guid? bookId, CancellationToken cancellationToken) =>
            Task.FromResult(new AnnotationExportFile("a,b\r\n"u8.ToArray(), "text/csv; charset=utf-8", "export.csv"));
    }
}

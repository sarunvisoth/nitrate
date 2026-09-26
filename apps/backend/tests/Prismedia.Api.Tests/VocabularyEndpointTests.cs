using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Prismedia.Application.Vocabulary;
using Prismedia.Contracts.System;
using Prismedia.Contracts.Vocabulary;
using Prismedia.Domain.Entities;

namespace Prismedia.Api.Tests;

public sealed class VocabularyEndpointTests {
    [Fact]
    public async Task ListBindsFiltersAndDecodesTheStatus() {
        var service = new FakeVocabulary();
        using var factory = CreateFactory(service);
        using var client = factory.CreateAuthenticatedClient();
        var bookId = Guid.NewGuid();

        using var response = await client.GetAsync(
            $"/api/vocabulary?search=resile&status={VocabularyStatus.Learning.ToCode()}&bookId={bookId}&limit=25");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new VocabularyQuery("resile", VocabularyStatus.Learning, bookId, null, 25), service.LastQuery);
    }

    [Fact]
    public async Task UnknownStatusOrFormatIsABadRequest() {
        using var factory = CreateFactory(new FakeVocabulary());
        using var client = factory.CreateAuthenticatedClient();

        using var badStatus = await client.GetAsync("/api/vocabulary?status=mastered");
        using var badFormat = await client.GetAsync("/api/vocabulary/export?format=docx");

        Assert.Equal(HttpStatusCode.BadRequest, badStatus.StatusCode);
        Assert.Equal(ApiProblemCodes.RequestInvalid, (await badStatus.Content.ReadFromJsonAsync<ApiProblem>())!.Code);
        Assert.Equal(HttpStatusCode.BadRequest, badFormat.StatusCode);
    }

    [Fact]
    public async Task PutStoresAndReturnsTheEntry() {
        var service = new FakeVocabulary();
        using var factory = CreateFactory(service);
        using var client = factory.CreateAuthenticatedClient();
        var id = Guid.NewGuid();
        var request = new UpsertVocabularyEntryRequest("resile", "en", "to draw back", null, null, "context", "new", DateTimeOffset.UnixEpoch);

        using var response = await client.PutAsJsonAsync($"/api/vocabulary/{id}", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(id, service.LastUpsertId);
        Assert.Equal(request, service.LastRequest);
    }

    [Fact]
    public async Task DeleteMissingEntryIsNotFound() {
        using var factory = CreateFactory(new FakeVocabulary(notFound: true));
        using var client = factory.CreateAuthenticatedClient();

        using var response = await client.DeleteAsync($"/api/vocabulary/{Guid.NewGuid()}?updatedAt={Uri.EscapeDataString(DateTimeOffset.UnixEpoch.ToString("O"))}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ApiProblemCodes.VocabularyNotFound, (await response.Content.ReadFromJsonAsync<ApiProblem>())!.Code);
    }

    [Fact]
    public async Task ExportDownloadsAnAttachment() {
        using var factory = CreateFactory(new FakeVocabulary());
        using var client = factory.CreateAuthenticatedClient();

        using var response = await client.GetAsync($"/api/vocabulary/export?format={AnnotationExportFormat.Csv.ToCode()}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("word,def\r\n", await response.Content.ReadAsStringAsync());
    }

    private static WebApplicationFactory<Program> CreateFactory(IVocabularyService service) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.ConfigureServices(services => {
                services.RemoveAll<IVocabularyService>();
                services.AddSingleton(service);
            }))
            .WithTestAuth();

    private sealed class FakeVocabulary(bool notFound = false) : IVocabularyService {
        public VocabularyQuery? LastQuery { get; private set; }
        public Guid? LastUpsertId { get; private set; }
        public UpsertVocabularyEntryRequest? LastRequest { get; private set; }

        public Task<VocabularyListResponse> ListAsync(VocabularyQuery query, CancellationToken cancellationToken) {
            LastQuery = query;
            return Task.FromResult(new VocabularyListResponse([], null, 0));
        }

        public Task<VocabularyWriteResult> UpsertAsync(
            Guid entryId, UpsertVocabularyEntryRequest request, CancellationToken cancellationToken) {
            LastUpsertId = entryId;
            LastRequest = request;
            var entry = new VocabularyEntry(entryId, request.Word, request.Language, request.Definition, null, null,
                request.SourceLocator, request.SourceSentence, request.Status, DateTimeOffset.UnixEpoch, request.UpdatedAt);
            return Task.FromResult(new VocabularyWriteResult(VocabularyWriteStatus.Applied, entry, null));
        }

        public Task<VocabularyWriteResult> DeleteAsync(
            Guid entryId, DateTimeOffset updatedAt, CancellationToken cancellationToken) =>
            Task.FromResult(notFound
                ? new VocabularyWriteResult(VocabularyWriteStatus.NotFound, null, null)
                : new VocabularyWriteResult(VocabularyWriteStatus.Applied, null, null));

        public Task<VocabularyExportFile> ExportAsync(
            AnnotationExportFormat format, VocabularyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new VocabularyExportFile("word,def\r\n"u8.ToArray(), "text/csv; charset=utf-8", "vocabulary.csv"));
    }
}

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Prismedia.Application.Books;
using Prismedia.Application.Vocabulary;
using Prismedia.Contracts.Books;
using Prismedia.Contracts.Search;
using Prismedia.Contracts.Vocabulary;
using Prismedia.Domain.Entities;

namespace Prismedia.Api.Tests;

/// <summary>
/// Binding and short-circuit behavior for the unified reader-search endpoint. Per-user isolation
/// and library-visibility filtering are exercised where the query logic actually lives —
/// <c>EfAnnotationLibraryServiceTests</c> and <c>EfVocabularyServiceTests</c> — since this endpoint
/// only forwards to those unchanged services.
/// </summary>
public sealed class ReaderSearchEndpointTests {
    [Fact]
    public async Task ForwardsTheQueryAndLimitToBothServices() {
        var annotations = new FakeAnnotationLibrary();
        var vocabulary = new FakeVocabulary();
        using var factory = CreateFactory(annotations, vocabulary);
        using var client = factory.CreateAuthenticatedClient();

        using var response = await client.GetAsync("/api/search/reader?q=dune&limit=7");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new AnnotationLibraryQuery("dune", null, null, false, null, 7), annotations.LastQuery);
        Assert.Equal(new VocabularyQuery("dune", null, null, null, 7), vocabulary.LastQuery);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a")]
    public async Task BlankOrTooShortQueryShortCircuitsWithoutCallingEitherService(string? query) {
        var annotations = new FakeAnnotationLibrary();
        var vocabulary = new FakeVocabulary();
        using var factory = CreateFactory(annotations, vocabulary);
        using var client = factory.CreateAuthenticatedClient();

        var url = query is null ? "/api/search/reader" : $"/api/search/reader?q={Uri.EscapeDataString(query)}";
        var body = await client.GetFromJsonAsync<ReaderSearchResponse>(url);

        Assert.Empty(body!.Annotations);
        Assert.Empty(body.Vocabulary);
        Assert.Null(annotations.LastQuery);
        Assert.Null(vocabulary.LastQuery);
    }

    [Fact]
    public async Task LimitIsClampedToTheMaximum() {
        var annotations = new FakeAnnotationLibrary();
        var vocabulary = new FakeVocabulary();
        using var factory = CreateFactory(annotations, vocabulary);
        using var client = factory.CreateAuthenticatedClient();

        using var response = await client.GetAsync("/api/search/reader?q=dune&limit=999");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(50, annotations.LastQuery!.Limit);
        Assert.Equal(50, vocabulary.LastQuery!.Limit);
    }

    [Fact]
    public async Task ReturnsWhatEachServiceFound() {
        var annotationItem = new AnnotationLibraryItem(
            Guid.NewGuid(), Guid.NewGuid(), "Dune", "epub-cfi", "/6/4", 0, 0.1, "the spice", null,
            "highlight", "yellow", false, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        var vocabularyItem = new VocabularyEntry(
            Guid.NewGuid(), "spice", "en", "a flavoring", null, null, null, null, "new",
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        var annotations = new FakeAnnotationLibrary { Response = new AnnotationLibraryResponse([annotationItem], null, 1) };
        var vocabulary = new FakeVocabulary { Response = new VocabularyListResponse([vocabularyItem], null, 1) };
        using var factory = CreateFactory(annotations, vocabulary);
        using var client = factory.CreateAuthenticatedClient();

        var body = await client.GetFromJsonAsync<ReaderSearchResponse>("/api/search/reader?q=spice");

        Assert.Equal("spice", Assert.Single(body!.Vocabulary).Word);
        Assert.Equal("the spice", Assert.Single(body.Annotations).SelectedText);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        IAnnotationLibraryService annotations,
        IVocabularyService vocabulary) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.ConfigureServices(services => {
                services.RemoveAll<IAnnotationLibraryService>();
                services.AddSingleton(annotations);
                services.RemoveAll<IVocabularyService>();
                services.AddSingleton(vocabulary);
            }))
            .WithTestAuth();

    private sealed class FakeAnnotationLibrary : IAnnotationLibraryService {
        public AnnotationLibraryQuery? LastQuery { get; private set; }
        public AnnotationLibraryResponse Response { get; set; } = new([], null, 0);

        public Task<AnnotationLibraryResponse> ListAsync(AnnotationLibraryQuery query, CancellationToken cancellationToken) {
            LastQuery = query;
            return Task.FromResult(Response);
        }

        public Task<AnnotationLibraryBooksResponse> BooksAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new AnnotationLibraryBooksResponse([]));

        public Task<AnnotationExportFile> ExportAsync(AnnotationExportFormat format, Guid? bookId, CancellationToken cancellationToken) =>
            Task.FromResult(new AnnotationExportFile([], "text/csv", "export.csv"));
    }

    private sealed class FakeVocabulary : IVocabularyService {
        public VocabularyQuery? LastQuery { get; private set; }
        public VocabularyListResponse Response { get; set; } = new([], null, 0);

        public Task<VocabularyListResponse> ListAsync(VocabularyQuery query, CancellationToken cancellationToken) {
            LastQuery = query;
            return Task.FromResult(Response);
        }

        public Task<VocabularyWriteResult> UpsertAsync(Guid entryId, UpsertVocabularyEntryRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<VocabularyWriteResult> DeleteAsync(Guid entryId, DateTimeOffset updatedAt, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<VocabularyExportFile> ExportAsync(AnnotationExportFormat format, VocabularyQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}

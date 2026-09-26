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

public sealed class BookAnnotationEndpointTests {
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ListsTheCallersAnnotations() {
        var bookId = Guid.NewGuid();
        var service = new FakeBookAnnotationService(bookId) { Stored = Annotation(bookId, Guid.NewGuid()) };
        using var factory = CreateFactory(service);
        using var client = factory.CreateAuthenticatedClient();

        using var response = await client.GetAsync($"/api/books/{bookId}/annotations");
        var body = await response.Content.ReadFromJsonAsync<BookAnnotationsResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(service.Stored!.Id, Assert.Single(body!.Annotations).Id);
    }

    [Fact]
    public async Task ListOfAnUnknownBookIsNotFound() {
        using var factory = CreateFactory(new FakeBookAnnotationService(Guid.NewGuid()));
        using var client = factory.CreateAuthenticatedClient();

        using var response = await client.GetAsync($"/api/books/{Guid.NewGuid()}/annotations");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(BookAnnotationWriteStatus.Applied, HttpStatusCode.OK)]
    [InlineData(BookAnnotationWriteStatus.Superseded, HttpStatusCode.OK)]
    [InlineData(BookAnnotationWriteStatus.NotFound, HttpStatusCode.NotFound)]
    [InlineData(BookAnnotationWriteStatus.Invalid, HttpStatusCode.BadRequest)]
    public async Task UpsertMapsEachOutcome(BookAnnotationWriteStatus status, HttpStatusCode expected) {
        var bookId = Guid.NewGuid();
        var annotationId = Guid.NewGuid();
        var service = new FakeBookAnnotationService(bookId) { WriteStatus = status };
        using var factory = CreateFactory(service);
        using var client = factory.CreateAuthenticatedClient();

        using var response = await client.PutAsJsonAsync(
            $"/api/books/{bookId}/annotations/{annotationId}",
            new UpsertBookAnnotationRequest(
                AnnotationLocatorKind.EpubCfi.ToCode(), "epubcfi(/6/4!/4/2,/1:0,/1:5)", 1, 0.1, "Hello", null, null,
                AnnotationStyle.Highlight.ToCode(), AnnotationColor.Yellow.ToCode(), null, Now));

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(annotationId, service.LastAnnotationId);
        if (status == BookAnnotationWriteStatus.Invalid) {
            var problem = await response.Content.ReadFromJsonAsync<ApiProblem>();
            Assert.Equal(ApiProblemCodes.InvalidBookAnnotation, problem!.Code);
        } else if (expected == HttpStatusCode.OK) {
            var body = await response.Content.ReadFromJsonAsync<BookAnnotation>();
            Assert.Equal(annotationId, body!.Id);
        }
    }

    [Fact]
    public async Task DeleteReturnsTheTombstone() {
        var bookId = Guid.NewGuid();
        var annotationId = Guid.NewGuid();
        var service = new FakeBookAnnotationService(bookId);
        using var factory = CreateFactory(service);
        using var client = factory.CreateAuthenticatedClient();

        using var response = await client.DeleteAsync(
            $"/api/books/{bookId}/annotations/{annotationId}?updatedAt={Uri.EscapeDataString(Now.ToString("O"))}");
        var body = await response.Content.ReadFromJsonAsync<BookAnnotation>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Now, body!.DeletedAt);
        Assert.Equal(Now, service.LastDeleteClock);
    }

    [Fact]
    public async Task DeleteWithoutAClockIsABadRequest() {
        var bookId = Guid.NewGuid();
        using var factory = CreateFactory(new FakeBookAnnotationService(bookId));
        using var client = factory.CreateAuthenticatedClient();

        using var response = await client.DeleteAsync($"/api/books/{bookId}/annotations/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousCallersAreRejected() {
        var bookId = Guid.NewGuid();
        using var factory = CreateFactory(new FakeBookAnnotationService(bookId));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/books/{bookId}/annotations");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static BookAnnotation Annotation(Guid bookId, Guid id, DateTimeOffset? deletedAt = null) => new(
        id, bookId, AnnotationLocatorKind.EpubCfi.ToCode(), "epubcfi(/6/4!/4/2,/1:0,/1:5)", 1, 0.1, "Hello", null, null,
        AnnotationStyle.Highlight.ToCode(), AnnotationColor.Yellow.ToCode(), null, null, Now, Now, deletedAt);

    private static WebApplicationFactory<Program> CreateFactory(IBookAnnotationService service) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => {
                builder.ConfigureServices(services => {
                    services.RemoveAll<IBookAnnotationService>();
                    services.AddSingleton(service);
                });
            })
            .WithTestAuth();

    private sealed class FakeBookAnnotationService(Guid bookId) : IBookAnnotationService {
        public BookAnnotation? Stored { get; init; }
        public BookAnnotationWriteStatus WriteStatus { get; init; } = BookAnnotationWriteStatus.Applied;
        public Guid? LastAnnotationId { get; private set; }
        public DateTimeOffset? LastDeleteClock { get; private set; }

        public Task<BookAnnotationsResponse?> ListAsync(Guid requestedBookId, CancellationToken cancellationToken) =>
            Task.FromResult<BookAnnotationsResponse?>(requestedBookId == bookId
                ? new BookAnnotationsResponse(Stored is null ? [] : [Stored])
                : null);

        public Task<BookAnnotationWriteResult> UpsertAsync(
            Guid requestedBookId,
            Guid annotationId,
            UpsertBookAnnotationRequest request,
            CancellationToken cancellationToken) {
            LastAnnotationId = annotationId;
            return Task.FromResult(WriteStatus switch {
                BookAnnotationWriteStatus.Invalid => new BookAnnotationWriteResult(WriteStatus, null, "Unknown color."),
                BookAnnotationWriteStatus.NotFound => new BookAnnotationWriteResult(WriteStatus, null, null),
                _ => new BookAnnotationWriteResult(WriteStatus, Annotation(requestedBookId, annotationId), null)
            });
        }

        public Task<BookAnnotationWriteResult> DeleteAsync(
            Guid requestedBookId,
            Guid annotationId,
            DateTimeOffset updatedAt,
            CancellationToken cancellationToken) {
            LastDeleteClock = updatedAt;
            return Task.FromResult(new BookAnnotationWriteResult(
                BookAnnotationWriteStatus.Applied,
                Annotation(requestedBookId, annotationId, updatedAt),
                null));
        }
    }
}

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

public sealed class BookBookmarkEndpointTests {
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ListsSavesAndDeletesBookmarks() {
        var bookId = Guid.NewGuid();
        var bookmarkId = Guid.NewGuid();
        var service = new FakeBookmarks(bookId);
        using var factory = CreateFactory(service);
        using var client = factory.CreateAuthenticatedClient();

        using var put = await client.PutAsJsonAsync(
            $"/api/books/{bookId}/bookmarks/{bookmarkId}",
            new UpsertBookBookmarkRequest(
                AnnotationLocatorKind.EpubCfi.ToCode(), "epubcfi(/6/4!/4/2/1:0)", 1, 0.2, "Chapter", "Label", Now));
        using var list = await client.GetAsync($"/api/books/{bookId}/bookmarks");
        using var delete = await client.DeleteAsync(
            $"/api/books/{bookId}/bookmarks/{bookmarkId}?updatedAt={Uri.EscapeDataString(Now.ToString("O"))}");

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal("Label", (await put.Content.ReadFromJsonAsync<BookBookmark>())!.Label);
        Assert.Equal(bookmarkId, Assert.Single((await list.Content.ReadFromJsonAsync<BookBookmarksResponse>())!.Bookmarks).Id);
        Assert.Equal(Now, (await delete.Content.ReadFromJsonAsync<BookBookmark>())!.DeletedAt);
    }

    [Fact]
    public async Task InvalidAndMissingMapToProblems() {
        var bookId = Guid.NewGuid();
        using var factory = CreateFactory(new FakeBookmarks(bookId) { Status = BookAnnotationWriteStatus.Invalid });
        using var client = factory.CreateAuthenticatedClient();

        using var invalid = await client.PutAsJsonAsync(
            $"/api/books/{bookId}/bookmarks/{Guid.NewGuid()}",
            new UpsertBookBookmarkRequest("x", "y", 0, 0, null, null, Now));
        using var missing = await client.GetAsync($"/api/books/{Guid.NewGuid()}/bookmarks");
        using var noClock = await client.DeleteAsync($"/api/books/{bookId}/bookmarks/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(ApiProblemCodes.InvalidBookBookmark, (await invalid.Content.ReadFromJsonAsync<ApiProblem>())!.Code);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noClock.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(IBookBookmarkService service) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.ConfigureServices(services => {
                services.RemoveAll<IBookBookmarkService>();
                services.AddSingleton(service);
            }))
            .WithTestAuth();

    private sealed class FakeBookmarks(Guid bookId) : IBookBookmarkService {
        private readonly List<BookBookmark> _stored = [];
        public BookAnnotationWriteStatus Status { get; init; } = BookAnnotationWriteStatus.Applied;

        public Task<BookBookmarksResponse?> ListAsync(Guid requested, CancellationToken cancellationToken) =>
            Task.FromResult(requested == bookId ? new BookBookmarksResponse(_stored) : null);

        public Task<BookBookmarkWriteResult> UpsertAsync(
            Guid requested, Guid id, UpsertBookBookmarkRequest request, CancellationToken cancellationToken) {
            if (Status != BookAnnotationWriteStatus.Applied) {
                return Task.FromResult(new BookBookmarkWriteResult(Status, null, "bad"));
            }

            var bookmark = new BookBookmark(id, requested, request.LocatorKind, request.Locator, request.SpineIndex,
                request.Position, request.ChapterLabel, request.Label, request.UpdatedAt, request.UpdatedAt, null);
            _stored.Add(bookmark);
            return Task.FromResult(new BookBookmarkWriteResult(Status, bookmark, null));
        }

        public Task<BookBookmarkWriteResult> DeleteAsync(
            Guid requested, Guid id, DateTimeOffset updatedAt, CancellationToken cancellationToken) {
            var existing = _stored.First(b => b.Id == id);
            return Task.FromResult(new BookBookmarkWriteResult(
                BookAnnotationWriteStatus.Applied, existing with { DeletedAt = updatedAt, UpdatedAt = updatedAt }, null));
        }
    }
}

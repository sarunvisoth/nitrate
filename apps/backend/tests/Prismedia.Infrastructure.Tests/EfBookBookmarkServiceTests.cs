using Microsoft.EntityFrameworkCore;
using Prismedia.Application.Books;
using Prismedia.Application.Entities;
using Prismedia.Contracts.Books;
using Prismedia.Domain.Entities;
using Prismedia.Infrastructure.Media.Books;
using Prismedia.Infrastructure.Persistence;
using Prismedia.Infrastructure.Persistence.Entities;

namespace Prismedia.Infrastructure.Tests;

public sealed class EfBookBookmarkServiceTests {
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
    private readonly Guid _bookId = Guid.NewGuid();
    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _userB = Guid.NewGuid();
    private readonly string _databaseName = $"book-bookmarks-{Guid.NewGuid():N}";

    [Fact]
    public async Task UpsertThenListReturnsTheBookmarkForItsOwnerOnly() {
        await SeedBookAsync();
        var id = Guid.NewGuid();

        var write = await Service(_userA).UpsertAsync(_bookId, id, Request(Now, label: "Great scene"), CancellationToken.None);
        var own = await Service(_userA).ListAsync(_bookId, CancellationToken.None);
        var other = await Service(_userB).ListAsync(_bookId, CancellationToken.None);

        Assert.Equal(BookAnnotationWriteStatus.Applied, write.Status);
        var stored = Assert.Single(own!.Bookmarks);
        Assert.Equal("Great scene", stored.Label);
        Assert.Equal("The Harbour", stored.ChapterLabel);
        Assert.Equal(AnnotationLocatorKind.EpubCfi.ToCode(), stored.LocatorKind);
        Assert.Empty(other!.Bookmarks);
    }

    [Fact]
    public async Task OlderClockIsSupersededAndNewerKeepsCreatedAt() {
        await SeedBookAsync();
        var id = Guid.NewGuid();
        var created = await Service(_userA).UpsertAsync(_bookId, id, Request(Now, label: "first"), CancellationToken.None);

        var stale = await Service(_userA).UpsertAsync(_bookId, id, Request(Now.AddMinutes(-1), label: "old"), CancellationToken.None);
        var renamed = await Service(_userA).UpsertAsync(_bookId, id, Request(Now.AddMinutes(1), label: "renamed"), CancellationToken.None);

        Assert.Equal(BookAnnotationWriteStatus.Superseded, stale.Status);
        Assert.Equal("first", stale.Bookmark!.Label);
        Assert.Equal("renamed", renamed.Bookmark!.Label);
        Assert.Equal(created.Bookmark!.CreatedAt, renamed.Bookmark.CreatedAt);
    }

    [Fact]
    public async Task DeleteTombstonesAndAnotherUserCannotTouchIt() {
        await SeedBookAsync();
        var id = Guid.NewGuid();
        await Service(_userA).UpsertAsync(_bookId, id, Request(Now), CancellationToken.None);

        var hijack = await Service(_userB).DeleteAsync(_bookId, id, Now.AddMinutes(1), CancellationToken.None);
        var deleted = await Service(_userA).DeleteAsync(_bookId, id, Now.AddMinutes(1), CancellationToken.None);
        var list = await Service(_userA).ListAsync(_bookId, CancellationToken.None);

        Assert.Equal(BookAnnotationWriteStatus.NotFound, hijack.Status);
        Assert.Equal(BookAnnotationWriteStatus.Applied, deleted.Status);
        Assert.Equal(Now.AddMinutes(1), Assert.Single(list!.Bookmarks).DeletedAt);
    }

    [Fact]
    public async Task HiddenBookOrAnonymousCallerIsNotFound() {
        await SeedBookAsync();

        var hidden = await Service(_userA, visible: false).UpsertAsync(_bookId, Guid.NewGuid(), Request(Now), CancellationToken.None);
        var anonymous = new EfBookBookmarkService(
            CreateContext(), new Visibility(true), TestUserContext.Anonymous(), new FixedClock(Now));

        Assert.Equal(BookAnnotationWriteStatus.NotFound, hidden.Status);
        Assert.Null(await anonymous.ListAsync(_bookId, CancellationToken.None));
    }

    [Theory]
    [InlineData("pdf-quads", "epubcfi(/6/4!/4/2/1:0)", 0.1, 0)]
    [InlineData("epub-cfi", " ", 0.1, 0)]
    [InlineData("epub-cfi", "epubcfi(/6/4!/4/2/1:0)", -0.5, 0)]
    [InlineData("epub-cfi", "epubcfi(/6/4!/4/2/1:0)", 0.1, 3)]
    public async Task InvalidPayloadsAreRejected(string locatorKind, string locator, double position, int futureDays) {
        await SeedBookAsync();
        var request = new UpsertBookBookmarkRequest(locatorKind, locator, 1, position, null, null, Now.AddDays(futureDays));

        var result = await Service(_userA).UpsertAsync(_bookId, Guid.NewGuid(), request, CancellationToken.None);

        Assert.Equal(BookAnnotationWriteStatus.Invalid, result.Status);
    }

    private static UpsertBookBookmarkRequest Request(DateTimeOffset updatedAt, string? label = null) =>
        new(AnnotationLocatorKind.EpubCfi.ToCode(), "epubcfi(/6/4!/4/2/1:0)", 1, 0.2, "The Harbour", label, updatedAt);

    private EfBookBookmarkService Service(Guid userId, bool visible = true) =>
        new(CreateContext(), new Visibility(visible), TestUserContext.MemberAs(userId), new FixedClock(Now));

    private PrismediaDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<PrismediaDbContext>().UseInMemoryDatabase(_databaseName).Options);

    private async Task SeedBookAsync() {
        await using var db = CreateContext();
        db.Entities.Add(new EntityRow {
            Id = _bookId,
            KindCode = EntityKind.Book.ToCode(),
            Title = "Book",
            CreatedAt = Now,
            UpdatedAt = Now
        });
        await db.SaveChangesAsync();
    }

    private sealed class Visibility(bool visible) : IEntityVisibilityChecker {
        public Task<bool> IsVisibleAsync(Guid entityId, CancellationToken cancellationToken) => Task.FromResult(visible);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

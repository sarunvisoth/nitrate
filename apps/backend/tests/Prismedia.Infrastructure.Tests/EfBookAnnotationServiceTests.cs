using Microsoft.EntityFrameworkCore;
using Prismedia.Application.Books;
using Prismedia.Application.Entities;
using Prismedia.Application.Security;
using Prismedia.Contracts.Books;
using Prismedia.Domain.Entities;
using Prismedia.Infrastructure.Media.Books;
using Prismedia.Infrastructure.Persistence;
using Prismedia.Infrastructure.Persistence.Entities;

namespace Prismedia.Infrastructure.Tests;

public sealed class EfBookAnnotationServiceTests {
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
    private readonly Guid _bookId = Guid.NewGuid();
    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _userB = Guid.NewGuid();
    private readonly string _databaseName = $"book-annotations-{Guid.NewGuid():N}";

    [Fact]
    public async Task UpsertThenListReturnsTheAnnotationForItsOwnerOnly() {
        await SeedBookAsync();
        var id = Guid.NewGuid();

        var write = await Service(_userA).UpsertAsync(_bookId, id, Request(Now), CancellationToken.None);
        var ownList = await Service(_userA).ListAsync(_bookId, CancellationToken.None);
        var otherList = await Service(_userB).ListAsync(_bookId, CancellationToken.None);

        Assert.Equal(BookAnnotationWriteStatus.Applied, write.Status);
        var stored = Assert.Single(ownList!.Annotations);
        Assert.Equal(id, stored.Id);
        Assert.Equal(AnnotationColor.Yellow.ToCode(), stored.Color);
        Assert.Equal(AnnotationStyle.Highlight.ToCode(), stored.Style);
        Assert.Equal(AnnotationLocatorKind.EpubCfi.ToCode(), stored.LocatorKind);
        Assert.Empty(otherList!.Annotations);
    }

    [Fact]
    public async Task OlderUpdatedAtIsSupersededAndReturnsTheStoredVersion() {
        await SeedBookAsync();
        var id = Guid.NewGuid();
        await Service(_userA).UpsertAsync(_bookId, id, Request(Now, AnnotationColor.Blue), CancellationToken.None);

        var stale = await Service(_userA).UpsertAsync(
            _bookId, id, Request(Now.AddMinutes(-1), AnnotationColor.Red), CancellationToken.None);

        Assert.Equal(BookAnnotationWriteStatus.Superseded, stale.Status);
        Assert.Equal(AnnotationColor.Blue.ToCode(), stale.Annotation!.Color);
    }

    [Fact]
    public async Task NewerUpdatedAtReplacesAndKeepsCreatedAt() {
        await SeedBookAsync();
        var id = Guid.NewGuid();
        var created = await Service(_userA).UpsertAsync(_bookId, id, Request(Now), CancellationToken.None);

        var updated = await Service(_userA).UpsertAsync(
            _bookId, id, Request(Now.AddMinutes(1), AnnotationColor.Green, note: "Compare with rectified flow."),
            CancellationToken.None);

        Assert.Equal(BookAnnotationWriteStatus.Applied, updated.Status);
        Assert.Equal(AnnotationColor.Green.ToCode(), updated.Annotation!.Color);
        Assert.Equal("Compare with rectified flow.", updated.Annotation.Note);
        Assert.Equal(created.Annotation!.CreatedAt, updated.Annotation.CreatedAt);
    }

    [Fact]
    public async Task DeleteSetsDeletedAtAndListStillReturnsTheTombstone() {
        await SeedBookAsync();
        var id = Guid.NewGuid();
        await Service(_userA).UpsertAsync(_bookId, id, Request(Now), CancellationToken.None);

        var deleted = await Service(_userA).DeleteAsync(_bookId, id, Now.AddMinutes(1), CancellationToken.None);
        var list = await Service(_userA).ListAsync(_bookId, CancellationToken.None);

        Assert.Equal(BookAnnotationWriteStatus.Applied, deleted.Status);
        Assert.Equal(Now.AddMinutes(1), Assert.Single(list!.Annotations).DeletedAt);
    }

    [Fact]
    public async Task StaleDeleteIsSupersededByANewerEdit() {
        await SeedBookAsync();
        var id = Guid.NewGuid();
        await Service(_userA).UpsertAsync(_bookId, id, Request(Now.AddMinutes(5)), CancellationToken.None);

        var deleted = await Service(_userA).DeleteAsync(_bookId, id, Now, CancellationToken.None);

        Assert.Equal(BookAnnotationWriteStatus.Superseded, deleted.Status);
        Assert.Null(deleted.Annotation!.DeletedAt);
    }

    [Fact]
    public async Task UpsertingAnotherUsersAnnotationIsNotFoundAndLeavesItUntouched() {
        await SeedBookAsync();
        var id = Guid.NewGuid();
        await Service(_userA).UpsertAsync(_bookId, id, Request(Now), CancellationToken.None);

        var hijack = await Service(_userB).UpsertAsync(
            _bookId, id, Request(Now.AddMinutes(1), AnnotationColor.Red), CancellationToken.None);
        var delete = await Service(_userB).DeleteAsync(_bookId, id, Now.AddMinutes(1), CancellationToken.None);
        var ownList = await Service(_userA).ListAsync(_bookId, CancellationToken.None);

        Assert.Equal(BookAnnotationWriteStatus.NotFound, hijack.Status);
        Assert.Equal(BookAnnotationWriteStatus.NotFound, delete.Status);
        Assert.Equal(AnnotationColor.Yellow.ToCode(), Assert.Single(ownList!.Annotations).Color);
    }

    [Fact]
    public async Task NonBookOrHiddenBookIsNotFound() {
        await SeedBookAsync();
        var chapterId = Guid.NewGuid();
        await using (var db = CreateContext()) {
            db.Entities.Add(new EntityRow {
                Id = chapterId,
                KindCode = EntityKind.BookChapter.ToCode(),
                Title = "Chapter",
                CreatedAt = Now,
                UpdatedAt = Now
            });
            await db.SaveChangesAsync();
        }

        var notABook = await Service(_userA).UpsertAsync(chapterId, Guid.NewGuid(), Request(Now), CancellationToken.None);
        var hidden = await Service(_userA, visible: false).UpsertAsync(_bookId, Guid.NewGuid(), Request(Now), CancellationToken.None);
        var hiddenList = await Service(_userA, visible: false).ListAsync(_bookId, CancellationToken.None);

        Assert.Equal(BookAnnotationWriteStatus.NotFound, notABook.Status);
        Assert.Equal(BookAnnotationWriteStatus.NotFound, hidden.Status);
        Assert.Null(hiddenList);
    }

    [Fact]
    public async Task AnonymousUserGetsNotFound() {
        await SeedBookAsync();
        var anonymous = new EfBookAnnotationService(
            CreateContext(), new Visibility(true), TestUserContext.Anonymous(), new FixedClock(Now));

        Assert.Null(await anonymous.ListAsync(_bookId, CancellationToken.None));
        Assert.Equal(
            BookAnnotationWriteStatus.NotFound,
            (await anonymous.UpsertAsync(_bookId, Guid.NewGuid(), Request(Now), CancellationToken.None)).Status);
    }

    [Theory]
    [InlineData("orange", "highlight", "epub-cfi", "epubcfi(/6/4!/4/2,/1:0,/1:5)", 0.1, 0)]
    [InlineData("yellow", "zigzag", "epub-cfi", "epubcfi(/6/4!/4/2,/1:0,/1:5)", 0.1, 0)]
    [InlineData("yellow", "highlight", "pdf-quads", "epubcfi(/6/4!/4/2,/1:0,/1:5)", 0.1, 0)]
    [InlineData("yellow", "highlight", "epub-cfi", " ", 0.1, 0)]
    [InlineData("yellow", "highlight", "epub-cfi", "epubcfi(/6/4!/4/2,/1:0,/1:5)", 1.5, 0)]
    [InlineData("yellow", "highlight", "epub-cfi", "epubcfi(/6/4!/4/2,/1:0,/1:5)", 0.1, 3)]
    public async Task InvalidPayloadsAreRejected(
        string color, string style, string locatorKind, string locator, double position, int futureDays) {
        await SeedBookAsync();
        var request = new UpsertBookAnnotationRequest(
            locatorKind, locator, 1, position, "Hello", null, null, style, color, null, Now.AddDays(futureDays));

        var result = await Service(_userA).UpsertAsync(_bookId, Guid.NewGuid(), request, CancellationToken.None);

        Assert.Equal(BookAnnotationWriteStatus.Invalid, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public async Task PdfLocatorWithValidPageAndRectsIsAccepted() {
        await SeedBookAsync();
        var locator = PdfAnnotationLocatorCodec.Write(new PdfAnnotationLocator(
            4, [new PdfAnnotationRect(0.1, 0.2, 0.3, 0.05)]));
        var request = new UpsertBookAnnotationRequest(
            AnnotationLocatorKind.Pdf.ToCode(), locator, 4, 0.2, "Hello", null, null,
            AnnotationStyle.Highlight.ToCode(), AnnotationColor.Yellow.ToCode(), null, Now);

        var result = await Service(_userA).UpsertAsync(_bookId, Guid.NewGuid(), request, CancellationToken.None);

        Assert.Equal(BookAnnotationWriteStatus.Applied, result.Status);
        Assert.Equal(AnnotationLocatorKind.Pdf.ToCode(), result.Annotation!.LocatorKind);
        Assert.Equal(locator, result.Annotation.Locator);
    }

    [Theory]
    [InlineData("{\"page\":-1,\"rects\":[]}")] // negative page
    [InlineData("{\"page\":0,\"rects\":[{\"x\":1.5,\"y\":0,\"w\":0.1,\"h\":0.1}]}")] // rect out of 0..1
    [InlineData("not json")]
    public async Task PdfLocatorWithInvalidPayloadIsRejected(string locator) {
        await SeedBookAsync();
        var request = new UpsertBookAnnotationRequest(
            AnnotationLocatorKind.Pdf.ToCode(), locator, 0, 0.1, "Hello", null, null,
            AnnotationStyle.Highlight.ToCode(), AnnotationColor.Yellow.ToCode(), null, Now);

        var result = await Service(_userA).UpsertAsync(_bookId, Guid.NewGuid(), request, CancellationToken.None);

        Assert.Equal(BookAnnotationWriteStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task PdfLocatorWithTooManyRectsIsRejected() {
        await SeedBookAsync();
        var rects = Enumerable.Range(0, PdfAnnotationLocatorCodec.MaxRects + 1)
            .Select(_ => new PdfAnnotationRect(0, 0, 0.1, 0.1))
            .ToList();
        var locator = PdfAnnotationLocatorCodec.Write(new PdfAnnotationLocator(0, rects));
        var request = new UpsertBookAnnotationRequest(
            AnnotationLocatorKind.Pdf.ToCode(), locator, 0, 0.1, "Hello", null, null,
            AnnotationStyle.Highlight.ToCode(), AnnotationColor.Yellow.ToCode(), null, Now);

        var result = await Service(_userA).UpsertAsync(_bookId, Guid.NewGuid(), request, CancellationToken.None);

        Assert.Equal(BookAnnotationWriteStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task SourceSignatureIsCopiedFromTheBookContentStateOnCreate() {
        await SeedBookAsync();
        await using (var db = CreateContext()) {
            db.BookContentStates.Add(new BookContentStateRow {
                BookId = _bookId,
                SourceSignature = "epub:1:2:3",
                RefreshedAt = Now
            });
            await db.SaveChangesAsync();
        }

        var result = await Service(_userA).UpsertAsync(_bookId, Guid.NewGuid(), Request(Now), CancellationToken.None);

        Assert.Equal("epub:1:2:3", result.Annotation!.SourceSignature);
    }

    private UpsertBookAnnotationRequest Request(
        DateTimeOffset updatedAt,
        AnnotationColor color = AnnotationColor.Yellow,
        string? note = null) =>
        new(
            AnnotationLocatorKind.EpubCfi.ToCode(),
            "epubcfi(/6/4!/4/2,/1:0,/1:5)",
            1,
            0.1,
            "Hello",
            "Before ",
            " after",
            AnnotationStyle.Highlight.ToCode(),
            color.ToCode(),
            note,
            updatedAt);

    private EfBookAnnotationService Service(Guid userId, bool visible = true) =>
        new(CreateContext(), new Visibility(visible), TestUserContext.MemberAs(userId), new FixedClock(Now));

    private PrismediaDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<PrismediaDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .Options);

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
        public Task<bool> IsVisibleAsync(Guid entityId, CancellationToken cancellationToken) =>
            Task.FromResult(visible);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

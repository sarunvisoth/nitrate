using Microsoft.EntityFrameworkCore;
using Prismedia.Application.Books;
using Prismedia.Application.Entities;
using Prismedia.Domain.Entities;
using Prismedia.Infrastructure.Media.Books;
using Prismedia.Infrastructure.Persistence;
using Prismedia.Infrastructure.Persistence.Entities;

namespace Prismedia.Infrastructure.Tests;

public sealed class EfAnnotationLibraryServiceTests {
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _otherUser = Guid.NewGuid();
    private readonly Guid _bookA = Guid.NewGuid();
    private readonly Guid _bookB = Guid.NewGuid();
    private readonly Guid _hiddenBook = Guid.NewGuid();
    private readonly string _databaseName = $"annotation-library-{Guid.NewGuid():N}";

    [Fact]
    public async Task ListsOnlyTheCallersLiveAnnotationsOnVisibleBooksInReadingOrder() {
        await SeedAsync();

        var page = await Service().ListAsync(new AnnotationLibraryQuery(null, null, null, false, null, 50), CancellationToken.None);

        Assert.Equal(["Alpha first", "Alpha second", "Beta noted"], page.Items.Select(i => i.SelectedText));
        Assert.Equal(3, page.TotalCount);
        Assert.Equal("Book Alpha", page.Items[0].BookTitle);
    }

    [Fact]
    public async Task FiltersBySearchInTextOrNoteBookColourAndNotesOnly() {
        await SeedAsync();
        var service = Service();

        var byNote = await service.ListAsync(new AnnotationLibraryQuery("RECTIFIED", null, null, false, null, 50), CancellationToken.None);
        var byBook = await service.ListAsync(new AnnotationLibraryQuery(null, _bookA, null, false, null, 50), CancellationToken.None);
        var byColor = await service.ListAsync(new AnnotationLibraryQuery(null, null, AnnotationColor.Blue, false, null, 50), CancellationToken.None);
        var notesOnly = await service.ListAsync(new AnnotationLibraryQuery(null, null, null, true, null, 50), CancellationToken.None);

        Assert.Equal(["Beta noted"], byNote.Items.Select(i => i.SelectedText));
        Assert.Equal(2, byBook.Items.Count);
        Assert.Equal(["Alpha second"], byColor.Items.Select(i => i.SelectedText));
        Assert.Equal(["Beta noted"], notesOnly.Items.Select(i => i.SelectedText));
    }

    [Fact]
    public async Task PagesWithAnOpaqueCursor() {
        await SeedAsync();
        var service = Service();

        var first = await service.ListAsync(new AnnotationLibraryQuery(null, null, null, false, null, 2), CancellationToken.None);
        var second = await service.ListAsync(new AnnotationLibraryQuery(null, null, null, false, first.NextCursor, 2), CancellationToken.None);

        Assert.Equal(2, first.Items.Count);
        Assert.NotNull(first.NextCursor);
        Assert.Equal(["Beta noted"], second.Items.Select(i => i.SelectedText));
        Assert.Null(second.NextCursor);
    }

    [Fact]
    public async Task BooksSummarisesCountsAndFlagsChangedFiles() {
        await SeedAsync();

        var books = await Service().BooksAsync(CancellationToken.None);

        var alpha = Assert.Single(books.Books, b => b.BookId == _bookA);
        Assert.Equal(2, alpha.HighlightCount);
        Assert.Equal(0, alpha.NoteCount);
        Assert.Equal(1, alpha.StaleCount);
        var beta = Assert.Single(books.Books, b => b.BookId == _bookB);
        Assert.Equal(1, beta.NoteCount);
        Assert.DoesNotContain(books.Books, b => b.BookId == _hiddenBook);
    }

    [Fact]
    public async Task ExportScopesToOneBookAndNamesTheFile() {
        await SeedAsync();

        var file = await Service().ExportAsync(AnnotationExportFormat.Markdown, _bookB, CancellationToken.None);
        var text = System.Text.Encoding.UTF8.GetString(file.Content);

        Assert.Contains("Beta noted", text);
        Assert.DoesNotContain("Alpha first", text);
        Assert.EndsWith(".md", file.FileName);
    }

    [Fact]
    public async Task AnonymousCallersSeeNothing() {
        await SeedAsync();
        var service = new EfAnnotationLibraryService(CreateContext(), new Visibility(_hiddenBook), TestUserContext.Anonymous(), new FixedClock(Now));

        var page = await service.ListAsync(new AnnotationLibraryQuery(null, null, null, false, null, 50), CancellationToken.None);

        Assert.Empty(page.Items);
    }

    private EfAnnotationLibraryService Service() =>
        new(CreateContext(), new Visibility(_hiddenBook), TestUserContext.MemberAs(_user), new FixedClock(Now));

    private PrismediaDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<PrismediaDbContext>().UseInMemoryDatabase(_databaseName).Options);

    private async Task SeedAsync() {
        await using var db = CreateContext();
        db.Entities.AddRange(
            Book(_bookA, "Book Alpha"),
            Book(_bookB, "Book Beta"),
            Book(_hiddenBook, "Hidden"));
        db.BookContentStates.Add(new BookContentStateRow { BookId = _bookA, SourceSignature = "epub:new", RefreshedAt = Now });
        db.BookAnnotations.AddRange(
            Row(_user, _bookA, "Alpha second", 2, 0.5, AnnotationColor.Blue, null, signature: "epub:new"),
            Row(_user, _bookA, "Alpha first", 1, 0.1, AnnotationColor.Yellow, null, signature: "epub:old"),
            Row(_user, _bookB, "Beta noted", 0, 0.2, AnnotationColor.Green, "Compare with rectified flow"),
            Row(_user, _bookB, "Beta deleted", 0, 0.3, AnnotationColor.Green, null, deleted: true),
            Row(_user, _hiddenBook, "Hidden text", 0, 0.1, AnnotationColor.Red, null),
            Row(_otherUser, _bookA, "Someone else", 0, 0.1, AnnotationColor.Red, null));
        await db.SaveChangesAsync();
    }

    private static EntityRow Book(Guid id, string title) => new() {
        Id = id, KindCode = EntityKind.Book.ToCode(), Title = title, CreatedAt = Now, UpdatedAt = Now
    };

    private static BookAnnotationRow Row(
        Guid user, Guid book, string text, int spine, double position, AnnotationColor color, string? note,
        bool deleted = false, string? signature = null) => new() {
        Id = Guid.NewGuid(), UserId = user, BookId = book, LocatorKind = AnnotationLocatorKind.EpubCfi,
        Locator = $"epubcfi(/6/{spine * 2 + 2}!/4/2,/1:0,/1:5)", SpineIndex = spine, Position = position,
        SelectedText = text, Style = AnnotationStyle.Highlight, Color = color, Note = note,
        SourceSignature = signature, CreatedAt = Now, UpdatedAt = Now, DeletedAt = deleted ? Now : null
    };

    private sealed class Visibility(Guid hidden) : IEntityVisibilityChecker {
        public Task<bool> IsVisibleAsync(Guid entityId, CancellationToken cancellationToken) =>
            Task.FromResult(entityId != hidden);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

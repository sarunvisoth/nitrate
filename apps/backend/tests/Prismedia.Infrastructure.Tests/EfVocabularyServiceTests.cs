using Microsoft.EntityFrameworkCore;
using Prismedia.Application.Vocabulary;
using Prismedia.Contracts.Vocabulary;
using Prismedia.Domain.Entities;
using Prismedia.Infrastructure.Media.Reader;
using Prismedia.Infrastructure.Persistence;
using Prismedia.Infrastructure.Persistence.Entities;

namespace Prismedia.Infrastructure.Tests;

public sealed class EfVocabularyServiceTests {
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _userB = Guid.NewGuid();
    private readonly string _databaseName = $"vocabulary-{Guid.NewGuid():N}";

    [Fact]
    public async Task UpsertThenListReturnsTheEntryForItsOwnerOnly() {
        var id = Guid.NewGuid();

        var write = await Service(_userA).UpsertAsync(id, Request(Now, "resile"), CancellationToken.None);
        var ownList = await Service(_userA).ListAsync(Query(), CancellationToken.None);
        var otherList = await Service(_userB).ListAsync(Query(), CancellationToken.None);

        Assert.Equal(VocabularyWriteStatus.Applied, write.Status);
        var stored = Assert.Single(ownList.Items);
        Assert.Equal("resile", stored.Word);
        Assert.Equal(VocabularyStatus.New.ToCode(), stored.Status);
        Assert.Empty(otherList.Items);
    }

    [Fact]
    public async Task AddingAnExistingWordUpdatesItInsteadOfDuplicating() {
        await Service(_userA).UpsertAsync(Guid.NewGuid(), Request(Now, "Resile", definition: "to draw back"), CancellationToken.None);

        var second = await Service(_userA).UpsertAsync(
            Guid.NewGuid(), Request(Now.AddMinutes(1), "resile", definition: "to recoil"), CancellationToken.None);
        var list = await Service(_userA).ListAsync(Query(), CancellationToken.None);

        Assert.Equal(VocabularyWriteStatus.Applied, second.Status);
        var stored = Assert.Single(list.Items);
        Assert.Equal("to recoil", stored.Definition);
    }

    [Fact]
    public async Task SameWordDifferentLanguageIsNotADuplicate() {
        await Service(_userA).UpsertAsync(Guid.NewGuid(), Request(Now, "chat", language: "fr"), CancellationToken.None);
        await Service(_userA).UpsertAsync(Guid.NewGuid(), Request(Now, "chat", language: "en"), CancellationToken.None);

        var list = await Service(_userA).ListAsync(Query(), CancellationToken.None);

        Assert.Equal(2, list.TotalCount);
    }

    [Fact]
    public async Task OlderUpdatedAtIsSuperseded() {
        var id = Guid.NewGuid();
        await Service(_userA).UpsertAsync(id, Request(Now, "resile", status: VocabularyStatus.Learning), CancellationToken.None);

        var stale = await Service(_userA).UpsertAsync(
            id, Request(Now.AddMinutes(-1), "resile", status: VocabularyStatus.Known), CancellationToken.None);

        Assert.Equal(VocabularyWriteStatus.Superseded, stale.Status);
        Assert.Equal(VocabularyStatus.Learning.ToCode(), stale.Entry!.Status);
    }

    [Fact]
    public async Task DeleteTombstonesAndListNoLongerReturnsIt() {
        var id = Guid.NewGuid();
        await Service(_userA).UpsertAsync(id, Request(Now, "resile"), CancellationToken.None);

        var deleted = await Service(_userA).DeleteAsync(id, Now.AddMinutes(1), CancellationToken.None);
        var list = await Service(_userA).ListAsync(Query(), CancellationToken.None);

        Assert.Equal(VocabularyWriteStatus.Applied, deleted.Status);
        Assert.Empty(list.Items);
    }

    [Fact]
    public async Task DeletingAnotherUsersEntryIsNotFound() {
        var id = Guid.NewGuid();
        await Service(_userA).UpsertAsync(id, Request(Now, "resile"), CancellationToken.None);

        var hijack = await Service(_userB).DeleteAsync(id, Now.AddMinutes(1), CancellationToken.None);

        Assert.Equal(VocabularyWriteStatus.NotFound, hijack.Status);
    }

    [Fact]
    public async Task SearchFiltersByWordAndDefinition() {
        await Service(_userA).UpsertAsync(Guid.NewGuid(), Request(Now, "resile", definition: "draw back"), CancellationToken.None);
        await Service(_userA).UpsertAsync(Guid.NewGuid(), Request(Now, "ebullient"), CancellationToken.None);

        var list = await Service(_userA).ListAsync(Query(search: "draw"), CancellationToken.None);

        Assert.Equal("resile", Assert.Single(list.Items).Word);
    }

    [Fact]
    public async Task StatusFilterNarrowsResults() {
        await Service(_userA).UpsertAsync(Guid.NewGuid(), Request(Now, "resile", status: VocabularyStatus.Known), CancellationToken.None);
        await Service(_userA).UpsertAsync(Guid.NewGuid(), Request(Now, "ebullient", status: VocabularyStatus.New), CancellationToken.None);

        var list = await Service(_userA).ListAsync(Query(status: VocabularyStatus.Known), CancellationToken.None);

        Assert.Equal("resile", Assert.Single(list.Items).Word);
    }

    [Theory]
    [InlineData("", "en", "new")]
    [InlineData("resile", "", "new")]
    [InlineData("resile", "en", "mastered")]
    public async Task InvalidPayloadsAreRejected(string word, string language, string status) {
        var result = await Service(_userA).UpsertAsync(
            Guid.NewGuid(),
            new UpsertVocabularyEntryRequest(word, language, null, null, null, null, status, Now),
            CancellationToken.None);

        Assert.Equal(VocabularyWriteStatus.Invalid, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public async Task ExportProducesAMarkdownFileWithTheWord() {
        await Service(_userA).UpsertAsync(Guid.NewGuid(), Request(Now, "resile"), CancellationToken.None);

        var file = await Service(_userA).ExportAsync(AnnotationExportFormat.Markdown, Query(), CancellationToken.None);

        Assert.Contains("resile", System.Text.Encoding.UTF8.GetString(file.Content));
    }

    [Fact]
    public async Task EntryReportsItsBookTitle() {
        var bookId = Guid.NewGuid();
        await using (var db = CreateContext()) {
            db.Entities.Add(new EntityRow {
                Id = bookId, KindCode = EntityKind.Book.ToCode(), Title = "Middlemarch", CreatedAt = Now, UpdatedAt = Now
            });
            await db.SaveChangesAsync();
        }

        await Service(_userA).UpsertAsync(
            Guid.NewGuid(),
            new UpsertVocabularyEntryRequest("resile", "en", null, bookId, "epubcfi(/6/4)", "context", "new", Now),
            CancellationToken.None);
        var list = await Service(_userA).ListAsync(Query(), CancellationToken.None);

        Assert.Equal("Middlemarch", Assert.Single(list.Items).BookTitle);
    }

    private static VocabularyQuery Query(
        string? search = null, VocabularyStatus? status = null, Guid? bookId = null) =>
        new(search, status, bookId, null, 200);

    private static UpsertVocabularyEntryRequest Request(
        DateTimeOffset updatedAt,
        string word,
        string language = "en",
        string? definition = null,
        VocabularyStatus status = VocabularyStatus.New) =>
        new(word, language, definition, null, null, "a sentence", status.ToCode(), updatedAt);

    private EfVocabularyService Service(Guid userId) =>
        new(CreateContext(), TestUserContext.MemberAs(userId), new FixedClock(Now));

    private PrismediaDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<PrismediaDbContext>().UseInMemoryDatabase(_databaseName).Options);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

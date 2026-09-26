using Microsoft.EntityFrameworkCore;
using Prismedia.Contracts.Reader;
using Prismedia.Domain.Entities;
using Prismedia.Infrastructure.Media.Reader;
using Prismedia.Infrastructure.Persistence;

namespace Prismedia.Infrastructure.Tests;

public sealed class EfReaderPreferencesServiceTests {
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _userB = Guid.NewGuid();
    private readonly string _databaseName = $"reader-preferences-{Guid.NewGuid():N}";

    [Fact]
    public async Task GetWithNoRowReturnsDefaults() {
        var response = await Service(_userA).GetAsync(CancellationToken.None);

        Assert.Equal(EfReaderPreferencesService.Defaults, response);
    }

    [Fact]
    public async Task AnonymousGetsDefaults() {
        var response = await new EfReaderPreferencesService(CreateContext(), TestUserContext.Anonymous())
            .GetAsync(CancellationToken.None);

        Assert.Equal(EfReaderPreferencesService.Defaults, response);
    }

    [Fact]
    public async Task UpsertThenGetRoundTripsAndIsIsolatedPerUser() {
        var write = await Service(_userA).UpsertAsync(Request(Now), CancellationToken.None);
        var ownGet = await Service(_userA).GetAsync(CancellationToken.None);
        var otherGet = await Service(_userB).GetAsync(CancellationToken.None);

        Assert.Equal(ReaderPreferencesWriteStatus.Applied, write.Status);
        Assert.Equal(ReaderTheme.Sepia.ToCode(), ownGet.Theme);
        Assert.Equal(140, ownGet.FontSizePercent);
        Assert.Equal(EfReaderPreferencesService.Defaults.Theme, otherGet.Theme);
    }

    [Fact]
    public async Task OlderUpdatedAtIsSuperseded() {
        await Service(_userA).UpsertAsync(Request(Now, ReaderTheme.Dark), CancellationToken.None);

        var stale = await Service(_userA).UpsertAsync(Request(Now.AddMinutes(-1), ReaderTheme.Light), CancellationToken.None);

        Assert.Equal(ReaderPreferencesWriteStatus.Superseded, stale.Status);
        Assert.Equal(ReaderTheme.Dark.ToCode(), stale.Preferences!.Theme);
    }

    [Theory]
    [InlineData("neon", 100, 1.5, 1, 900, "auto", "paged")]
    [InlineData("publisher", 79, 1.5, 1, 900, "auto", "paged")]
    [InlineData("publisher", 100, 2.5, 1, 900, "auto", "paged")]
    [InlineData("publisher", 100, 1.5, 9, 900, "auto", "paged")]
    [InlineData("publisher", 100, 1.5, 1, 100, "auto", "paged")]
    [InlineData("publisher", 100, 1.5, 1, 900, "rainbow", "paged")]
    [InlineData("publisher", 100, 1.5, 1, 900, "auto", "webtoon")]
    public async Task InvalidPayloadsAreRejected(
        string fontFamily, int fontSizePercent, double lineHeight, int marginStep, int maxContentWidth,
        string theme, string flow) {
        var request = new UpsertReaderPreferencesRequest(
            fontFamily, fontSizePercent, lineHeight, marginStep, maxContentWidth, theme, flow, null, Now);

        var result = await Service(_userA).UpsertAsync(request, CancellationToken.None);

        Assert.Equal(ReaderPreferencesWriteStatus.Invalid, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public async Task AnonymousUpsertIsInvalid() {
        var result = await new EfReaderPreferencesService(CreateContext(), TestUserContext.Anonymous())
            .UpsertAsync(Request(Now), CancellationToken.None);

        Assert.Equal(ReaderPreferencesWriteStatus.Invalid, result.Status);
    }

    private static UpsertReaderPreferencesRequest Request(DateTimeOffset updatedAt, ReaderTheme theme = ReaderTheme.Sepia) =>
        new(ReaderFontFamily.Serif.ToCode(), 140, 1.6, 2, 800, theme.ToCode(), ReaderMode.Scrolled.ToCode(), "fr", updatedAt);

    private EfReaderPreferencesService Service(Guid userId) =>
        new(CreateContext(), TestUserContext.MemberAs(userId));

    private PrismediaDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<PrismediaDbContext>().UseInMemoryDatabase(_databaseName).Options);
}

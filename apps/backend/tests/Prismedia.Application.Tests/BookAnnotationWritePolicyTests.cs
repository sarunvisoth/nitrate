using Prismedia.Application.Books;

namespace Prismedia.Application.Tests;

public sealed class BookAnnotationWritePolicyTests {
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FirstWriteAlwaysApplies() =>
        Assert.True(BookAnnotationWritePolicy.ShouldApply(stored: null, incoming: Now));

    [Fact]
    public void EqualOrNewerClockApplies() {
        Assert.True(BookAnnotationWritePolicy.ShouldApply(Now, Now));
        Assert.True(BookAnnotationWritePolicy.ShouldApply(Now, Now.AddMilliseconds(1)));
    }

    [Fact]
    public void OlderClockIsSuperseded() =>
        Assert.False(BookAnnotationWritePolicy.ShouldApply(Now, Now.AddSeconds(-1)));

    [Fact]
    public void ClocksFarInTheFutureAreRejected() {
        Assert.True(BookAnnotationWritePolicy.IsPlausibleClock(Now.AddHours(23), Now));
        Assert.False(BookAnnotationWritePolicy.IsPlausibleClock(Now.AddDays(2), Now));
    }
}

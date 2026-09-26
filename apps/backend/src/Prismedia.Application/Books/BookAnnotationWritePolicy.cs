namespace Prismedia.Application.Books;

/// <summary>Last-write-wins rules shared by every annotation write path.</summary>
public static class BookAnnotationWritePolicy {
    /// <summary>Maximum tolerated client clock lead over the server clock.</summary>
    public static readonly TimeSpan MaximumClockLead = TimeSpan.FromDays(1);

    /// <summary>True when an incoming write should replace the stored version.</summary>
    /// <param name="stored">Stored <c>updatedAt</c>, or null when nothing is stored yet.</param>
    /// <param name="incoming">Client-supplied <c>updatedAt</c> of the write.</param>
    /// <returns>True when the write is not older than the stored version.</returns>
    public static bool ShouldApply(DateTimeOffset? stored, DateTimeOffset incoming) =>
        stored is null || incoming >= stored.Value;

    /// <summary>True when a client clock is not implausibly far ahead of the server clock.</summary>
    /// <param name="incoming">Client-supplied <c>updatedAt</c>.</param>
    /// <param name="serverNow">Current server time.</param>
    /// <returns>False for clocks more than <see cref="MaximumClockLead"/> in the future.</returns>
    public static bool IsPlausibleClock(DateTimeOffset incoming, DateTimeOffset serverNow) =>
        incoming <= serverNow + MaximumClockLead;
}

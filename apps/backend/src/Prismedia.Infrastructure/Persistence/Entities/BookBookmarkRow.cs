using Prismedia.Domain.Entities;

namespace Prismedia.Infrastructure.Persistence.Entities;

/// <summary>One user's bookmark on a Book (per-user, many rows, tombstoned deletes).</summary>
public sealed class BookBookmarkRow {
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid BookId { get; set; }
    public AnnotationLocatorKind LocatorKind { get; set; }
    public string Locator { get; set; } = string.Empty;
    public int SpineIndex { get; set; }
    public double Position { get; set; }
    public string? ChapterLabel { get; set; }
    public string? Label { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

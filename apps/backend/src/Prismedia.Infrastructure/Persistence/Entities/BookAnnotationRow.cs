using Prismedia.Domain.Entities;

namespace Prismedia.Infrastructure.Persistence.Entities;

/// <summary>One user's annotation on a Book (per-user, many rows, tombstoned deletes).</summary>
public sealed class BookAnnotationRow {
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid BookId { get; set; }
    public AnnotationLocatorKind LocatorKind { get; set; }
    public string Locator { get; set; } = string.Empty;
    public int SpineIndex { get; set; }
    public double Position { get; set; }
    public string SelectedText { get; set; } = string.Empty;
    public string? TextBefore { get; set; }
    public string? TextAfter { get; set; }
    public AnnotationStyle Style { get; set; }
    public AnnotationColor Color { get; set; }
    public string? Note { get; set; }
    public string? SourceSignature { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

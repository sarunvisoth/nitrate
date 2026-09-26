using Prismedia.Domain.Entities;

namespace Prismedia.Infrastructure.Persistence.Entities;

/// <summary>One user's saved vocabulary word (per-user, many rows, tombstoned deletes).</summary>
public sealed class VocabularyEntryRow {
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Word { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string? Definition { get; set; }
    public Guid? BookId { get; set; }
    public string? SourceLocator { get; set; }
    public string? SourceSentence { get; set; }
    public VocabularyStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

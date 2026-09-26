using Prismedia.Domain.Entities;

namespace Prismedia.Infrastructure.Persistence.Entities;

/// <summary>One row per user: the reader's typography, theme, flow, and translation defaults.</summary>
public sealed class UserReaderPreferencesRow {
    public Guid UserId { get; set; }
    public ReaderFontFamily FontFamily { get; set; }
    public int FontSizePercent { get; set; }
    public double LineHeight { get; set; }
    public int MarginStep { get; set; }
    public int MaxContentWidth { get; set; }
    public ReaderTheme Theme { get; set; }
    public ReaderMode Flow { get; set; }
    public string? TranslationTargetLanguage { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

namespace Prismedia.Domain.Entities;

/// <summary>Closed set of reader font-family choices for reflowable Book text.</summary>
public enum ReaderFontFamily {
    /// <summary>Use whatever font the publisher's EPUB embeds or requests (default).</summary>
    [Code("publisher")]
    Publisher,

    /// <summary>Force a bundled serif font.</summary>
    [Code("serif")]
    Serif,

    /// <summary>Force a bundled sans-serif font.</summary>
    [Code("sans")]
    Sans,

    /// <summary>Force a bundled monospace font.</summary>
    [Code("monospace")]
    Monospace
}

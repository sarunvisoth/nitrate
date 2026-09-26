namespace Prismedia.Domain.Entities;

/// <summary>Closed set of reader colour themes.</summary>
public enum ReaderTheme {
    /// <summary>Follows the app-level light/dark preference (default).</summary>
    [Code("auto")]
    Auto,

    /// <summary>Forced light background and dark text.</summary>
    [Code("light")]
    Light,

    /// <summary>Forced dark background and light text.</summary>
    [Code("dark")]
    Dark,

    /// <summary>Warm low-contrast paper tone, kept at or above 4.5:1 text contrast.</summary>
    [Code("sepia")]
    Sepia
}

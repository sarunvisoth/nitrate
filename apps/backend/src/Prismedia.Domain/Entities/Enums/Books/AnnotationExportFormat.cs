namespace Prismedia.Domain.Entities;

/// <summary>File formats for exporting a user's highlights and notes.</summary>
public enum AnnotationExportFormat {
    /// <summary>Markdown: one section per book, each highlight quoted with its note.</summary>
    [Code("markdown")]
    Markdown,

    /// <summary>JSON: a stable machine-readable structure grouped by book.</summary>
    [Code("json")]
    Json,

    /// <summary>CSV (RFC 4180, UTF-8 with BOM): one row per highlight, for spreadsheets.</summary>
    [Code("csv")]
    Csv
}

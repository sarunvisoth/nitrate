namespace Prismedia.Domain.Entities;

/// <summary>
/// Provenance of an attached entity file, stored on <c>EntityFileRow.Source</c>. Each
/// member declares its stable storage code inline so <see cref="EnumCodec{TValue}"/>
/// derives the encode/decode mapping automatically.
/// </summary>
/// <remarks>
/// The column remains a plain string for storage compatibility; these codes are the
/// single source of truth for the values written to it. <see cref="Custom"/> covers any
/// user-curated or externally supplied asset (manual uploads and downloaded plugin
/// artwork alike) that scan-generated writes must never overwrite.
/// </remarks>
public enum FileSourceKind {
    /// <summary>Asset generated or discovered by the library scan/processing pipeline.</summary>
    [Code("scan")]
    Scan,

    /// <summary>User-curated or externally supplied asset that scan writes must not replace.</summary>
    [Code("custom")]
    Custom
}

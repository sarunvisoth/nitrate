using System.Text.Json;
using System.Text.Json.Serialization;

namespace Prismedia.Application.Books;

/// <summary>
/// One rect of a PDF highlight, normalized to the page's unscaled viewport (0-1 on both axes) so it survives
/// zoom, rotation, and device size changes. Coordinates are top-left origin, matching pdf.js's CSS viewport.
/// </summary>
public sealed record PdfAnnotationRect(
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y,
    [property: JsonPropertyName("w")] double Width,
    [property: JsonPropertyName("h")] double Height);

/// <summary>
/// The <c>locator</c> payload for a <see cref="Prismedia.Domain.Entities.AnnotationLocatorKind.Pdf"/> annotation
/// or bookmark: a 0-based page index plus the normalized rects the highlight covers (empty for a page bookmark).
/// Stored as JSON in the existing generic <c>Locator</c> column; the text-quote fields
/// (selectedText/textBefore/textAfter) remain in their own columns, shared with the EPUB locator kind.
/// </summary>
public sealed record PdfAnnotationLocator(
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("rects")] IReadOnlyList<PdfAnnotationRect> Rects);

/// <summary>Parses and validates the PDF locator JSON payload.</summary>
public static class PdfAnnotationLocatorCodec {
    /// <summary>Highlights with more rects than this are rejected (a generous bound on a real selection).</summary>
    public const int MaxRects = 64;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Serializes a locator to its stored JSON form.</summary>
    public static string Write(PdfAnnotationLocator locator) => JsonSerializer.Serialize(locator, Options);

    /// <summary>
    /// Parses and validates a stored/incoming PDF locator: <c>page</c> must be zero or greater, and there must be
    /// 0-<see cref="MaxRects"/> rects, each with every coordinate within 0-1. Returns null for anything else
    /// (malformed JSON, missing fields, out-of-range values) so callers can reject the write.
    /// </summary>
    public static PdfAnnotationLocator? TryParse(string json) {
        PdfAnnotationLocator? locator;
        try {
            locator = JsonSerializer.Deserialize<PdfAnnotationLocator>(json, Options);
        } catch (JsonException) {
            return null;
        }

        if (locator is null || locator.Page < 0 || locator.Rects.Count > MaxRects) {
            return null;
        }

        foreach (var rect in locator.Rects) {
            if (IsOutOfUnitRange(rect.X) || IsOutOfUnitRange(rect.Y) ||
                IsOutOfUnitRange(rect.Width) || IsOutOfUnitRange(rect.Height)) {
                return null;
            }
        }

        return locator;
    }

    private static bool IsOutOfUnitRange(double value) => double.IsNaN(value) || value is < 0 or > 1;
}

namespace Prismedia.Domain.Entities;

/// <summary>Format of an annotation's <c>locator</c> string.</summary>
public enum AnnotationLocatorKind {
    /// <summary>
    /// EPUB canonical fragment identifier for a text range, as produced by foliate-js <c>view.getCFI</c>.
    /// </summary>
    [Code("epub-cfi")]
    EpubCfi,

    /// <summary>
    /// PDF page + normalized rect locator (see <c>PdfAnnotationLocator</c>): a 0-based page index plus one or
    /// more rects normalized to the page's unscaled viewport (0-1), independent of zoom, rotation, or device
    /// size. Never reuses EPUB CFI semantics — PDFs have no spine or DOM range to anchor to.
    /// </summary>
    [Code("pdf")]
    Pdf
}

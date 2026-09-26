namespace Prismedia.Domain.Entities;

/// <summary>Format of an annotation's <c>locator</c> string.</summary>
public enum AnnotationLocatorKind {
    /// <summary>
    /// EPUB canonical fragment identifier for a text range, as produced by foliate-js <c>view.getCFI</c>.
    /// </summary>
    [Code("epub-cfi")]
    EpubCfi
}

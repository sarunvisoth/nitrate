using Prismedia.Application.Books;

namespace Prismedia.Application.Tests.Reader;

public sealed class PdfAnnotationLocatorTests {
    [Fact]
    public void RoundTripsThroughWriteAndTryParse() {
        var locator = new PdfAnnotationLocator(2, [new PdfAnnotationRect(0.1, 0.2, 0.3, 0.05)]);

        var parsed = PdfAnnotationLocatorCodec.TryParse(PdfAnnotationLocatorCodec.Write(locator));

        Assert.NotNull(parsed);
        Assert.Equal(2, parsed!.Page);
        Assert.Equal(0.1, parsed.Rects[0].X);
    }

    [Fact]
    public void AcceptsAPageBookmarkWithNoRects() {
        var parsed = PdfAnnotationLocatorCodec.TryParse(PdfAnnotationLocatorCodec.Write(new PdfAnnotationLocator(0, [])));

        Assert.NotNull(parsed);
        Assert.Empty(parsed!.Rects);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{\"page\":-1,\"rects\":[]}")]
    [InlineData("{\"page\":0,\"rects\":[{\"x\":-0.1,\"y\":0,\"w\":0.1,\"h\":0.1}]}")]
    [InlineData("{\"page\":0,\"rects\":[{\"x\":0,\"y\":0,\"w\":1.1,\"h\":0.1}]}")]
    public void RejectsMalformedOrOutOfRangePayloads(string json) {
        Assert.Null(PdfAnnotationLocatorCodec.TryParse(json));
    }

    [Fact]
    public void RejectsMoreThanTheMaximumRects() {
        var rects = Enumerable.Range(0, PdfAnnotationLocatorCodec.MaxRects + 1)
            .Select(_ => new PdfAnnotationRect(0, 0, 0.1, 0.1))
            .ToList();

        Assert.Null(PdfAnnotationLocatorCodec.TryParse(PdfAnnotationLocatorCodec.Write(new PdfAnnotationLocator(0, rects))));
    }

    [Fact]
    public void AcceptsExactlyTheMaximumRects() {
        var rects = Enumerable.Range(0, PdfAnnotationLocatorCodec.MaxRects)
            .Select(_ => new PdfAnnotationRect(0, 0, 0.1, 0.1))
            .ToList();

        Assert.NotNull(PdfAnnotationLocatorCodec.TryParse(PdfAnnotationLocatorCodec.Write(new PdfAnnotationLocator(0, rects))));
    }
}

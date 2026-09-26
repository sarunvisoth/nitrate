using Prismedia.Domain.Entities;

namespace Prismedia.Domain.Tests;

public sealed class AnnotationCodeTests {
    [Theory]
    [InlineData(AnnotationColor.Yellow, "yellow")]
    [InlineData(AnnotationColor.Blue, "blue")]
    [InlineData(AnnotationColor.Green, "green")]
    [InlineData(AnnotationColor.Red, "red")]
    [InlineData(AnnotationColor.Purple, "purple")]
    public void ColorsRoundTripThroughTheirCodes(AnnotationColor color, string code) {
        Assert.Equal(code, color.ToCode());
        Assert.Equal(color, code.DecodeAs<AnnotationColor>());
    }

    [Fact]
    public void StyleAndLocatorKindHaveStableCodes() {
        Assert.Equal("highlight", AnnotationStyle.Highlight.ToCode());
        Assert.Equal("epub-cfi", AnnotationLocatorKind.EpubCfi.ToCode());
    }

    [Fact]
    public void UnknownCodesDoNotDecode() {
        Assert.False("orange".TryDecodeAs<AnnotationColor>(out _));
    }
}

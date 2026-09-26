using Prismedia.Domain.Entities;

namespace Prismedia.Domain.Tests;

public sealed class ReaderVocabularyCodeTests {
    [Theory]
    [InlineData(ReaderFontFamily.Publisher, "publisher")]
    [InlineData(ReaderFontFamily.Serif, "serif")]
    [InlineData(ReaderFontFamily.Sans, "sans")]
    [InlineData(ReaderFontFamily.Monospace, "monospace")]
    public void FontFamiliesRoundTripThroughTheirCodes(ReaderFontFamily family, string code) {
        Assert.Equal(code, family.ToCode());
        Assert.Equal(family, code.DecodeAs<ReaderFontFamily>());
    }

    [Theory]
    [InlineData(ReaderTheme.Auto, "auto")]
    [InlineData(ReaderTheme.Light, "light")]
    [InlineData(ReaderTheme.Dark, "dark")]
    [InlineData(ReaderTheme.Sepia, "sepia")]
    public void ThemesRoundTripThroughTheirCodes(ReaderTheme theme, string code) {
        Assert.Equal(code, theme.ToCode());
        Assert.Equal(theme, code.DecodeAs<ReaderTheme>());
    }

    [Theory]
    [InlineData(VocabularyStatus.New, "new")]
    [InlineData(VocabularyStatus.Learning, "learning")]
    [InlineData(VocabularyStatus.Familiar, "familiar")]
    [InlineData(VocabularyStatus.Known, "known")]
    public void VocabularyStatusesRoundTripThroughTheirCodes(VocabularyStatus status, string code) {
        Assert.Equal(code, status.ToCode());
        Assert.Equal(status, code.DecodeAs<VocabularyStatus>());
    }

    [Fact]
    public void UnknownCodesDoNotDecode() {
        Assert.False("dyslexic".TryDecodeAs<ReaderFontFamily>(out _));
        Assert.False("solarized".TryDecodeAs<ReaderTheme>(out _));
        Assert.False("mastered".TryDecodeAs<VocabularyStatus>(out _));
    }
}

using Prismedia.Application.Jobs.Scanning;
using Prismedia.Application.Files;
using Prismedia.Contracts.Media;

namespace Prismedia.Application.Tests.Jobs.Scanning;

public sealed class SubtitleSidecarNameParserTests {
    [Fact]
    public void ExactVideoStemUsesUndeterminedLanguageWithoutALabel() {
        var parsed = SubtitleSidecarNameParser.Parse("Movie", "Movie");

        Assert.NotNull(parsed);
        Assert.Equal(SubtitleLanguages.Undetermined, parsed.Language);
        Assert.Null(parsed.Label);
    }

    [Theory]
    [InlineData("Movie.en", "en")]
    [InlineData("Movie.eng", "en")]
    [InlineData("Movie.pt-BR", "pt-BR")]
    [InlineData("Movie.pt_br", "pt-BR")]
    [InlineData("Movie.FR", "fr")]
    [InlineData("Movie.und", SubtitleLanguages.Undetermined)]
    public void LanguageTagsResolveToCanonicalCultureCodes(string sidecarStem, string language) {
        var parsed = SubtitleSidecarNameParser.Parse("Movie", sidecarStem);

        Assert.NotNull(parsed);
        Assert.Equal(language, parsed.Language);
        Assert.Null(parsed.Label);
    }

    [Fact]
    public void DescriptiveTagBecomesAHumanizedLabel() {
        var parsed = SubtitleSidecarNameParser.Parse("Movie", "Movie.ai_translated");

        Assert.NotNull(parsed);
        Assert.Equal(SubtitleLanguages.Undetermined, parsed.Language);
        Assert.Equal("Ai Translated", parsed.Label);
    }

    [Fact]
    public void LanguageAndDescriptiveTagsAreBothPreserved() {
        var parsed = SubtitleSidecarNameParser.Parse("Movie", "Movie.pt-BR.forced_sdh");

        Assert.NotNull(parsed);
        Assert.Equal("pt-BR", parsed.Language);
        Assert.Equal("Forced Sdh", parsed.Label);
    }

    [Theory]
    [InlineData("Movie 2")]
    [InlineData("MovieExtended")]
    [InlineData("Other.Movie")]
    public void NonDelimitedOrDifferentStemsDoNotMatch(string sidecarStem) =>
        Assert.Null(SubtitleSidecarNameParser.Parse("Movie", sidecarStem));

    [Fact]
    public void VideoStemComparisonUsesFileSystemPathSemantics() {
        var parsed = SubtitleSidecarNameParser.Parse("Movie", "movie.EN");

        if (FileSystemPathComparison.Comparison == StringComparison.OrdinalIgnoreCase) {
            Assert.NotNull(parsed);
            Assert.Equal("en", parsed.Language);
            return;
        }

        Assert.Null(parsed);
    }
}

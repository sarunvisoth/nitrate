using System.Text;
using Prismedia.Application.Jobs.Ports;
using Prismedia.Contracts.Media;
using Prismedia.Infrastructure.Media.Sidecars;

namespace Prismedia.Infrastructure.Tests;

public sealed class SubtitleSidecarDiscoveryTests : IDisposable {
    private const string EmptySha256 =
        "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("prismedia-subtitle-sidecars-");

    [Fact]
    public async Task BatchDiscoveryReturnsCanonicalCandidatesForEachVideo() {
        var movie = CreateFile("Movie.mkv");
        var directorsCut = CreateFile("Movie.Directors.Cut.MKV");
        var movieSrt = CreateFile("Movie.EN.SRT", "English");
        var movieVtt = CreateFile("Movie.fr.vtt", "French");
        var cutAss = CreateFile("Movie.Directors.Cut.pt_br.ASS", "Styled");
        var cutSsa = CreateFile("Movie.Directors.Cut.forced.ssa", "Forced");
        CreateFile("Movie.nfo", "ignored");

        var results = await new SubtitleSidecarDiscovery().DiscoverAsync(
            [movie, directorsCut],
            CancellationToken.None);

        Assert.Equal(2, results.Count);
        var movieResult = Assert.Single(results, result => result.VideoPath == movie);
        var cutResult = Assert.Single(results, result => result.VideoPath == directorsCut);
        Assert.True(movieResult.IsComplete);
        Assert.True(cutResult.IsComplete);
        Assert.Equal(
            new[] { movieSrt, movieVtt }.OrderBy(path => path, StringComparer.Ordinal),
            movieResult.Candidates.Select(candidate => candidate.Path).OrderBy(path => path, StringComparer.Ordinal));
        Assert.Equal(
            new[] { cutAss, cutSsa }.OrderBy(path => path, StringComparer.Ordinal),
            cutResult.Candidates.Select(candidate => candidate.Path).OrderBy(path => path, StringComparer.Ordinal));
        Assert.Equal(
            movieResult.Candidates.Select(candidate => candidate.SourceKey).OrderBy(key => key, StringComparer.Ordinal),
            movieResult.Candidates.Select(candidate => candidate.SourceKey));
        Assert.Equal(
            cutResult.Candidates.Select(candidate => candidate.SourceKey).OrderBy(key => key, StringComparer.Ordinal),
            cutResult.Candidates.Select(candidate => candidate.SourceKey));

        var english = Assert.Single(movieResult.Candidates, candidate => candidate.Path == movieSrt);
        Assert.Equal(ExpectedSourceKey("Movie.EN.SRT"), english.SourceKey);
        Assert.Equal(64, english.SourceKey.Length);
        Assert.Equal(english.SourceKey.ToLowerInvariant(), english.SourceKey);
        Assert.Equal(SubtitleFormats.Srt, english.Format);
        Assert.Equal("en", english.Language);
        Assert.Null(english.Label);
        Assert.Equal(Encoding.UTF8.GetByteCount("English"), english.SizeBytes);
        Assert.True(english.ModifiedTicks > 0);

        var portuguese = Assert.Single(cutResult.Candidates, candidate => candidate.Path == cutAss);
        Assert.Equal(SubtitleFormats.Ass, portuguese.Format);
        Assert.Equal("pt-BR", portuguese.Language);
        Assert.Null(portuguese.Label);

        var forced = Assert.Single(cutResult.Candidates, candidate => candidate.Path == cutSsa);
        Assert.Equal(SubtitleFormats.Ssa, forced.Format);
        Assert.Equal(SubtitleLanguages.Undetermined, forced.Language);
        Assert.Equal("Forced", forced.Label);
    }

    [Fact]
    public async Task LongerSiblingVideoOwnsItsSidecarEvenWhenItWasNotRequested() {
        var movie = CreateFile("Movie.mkv");
        CreateFile("Movie.Directors.Cut.mp4");
        CreateFile("Movie.Directors.Cut.en.srt", "English");

        var result = Assert.Single(await new SubtitleSidecarDiscovery().DiscoverAsync(
            [movie],
            CancellationToken.None));

        Assert.Empty(result.Candidates);
        Assert.Equal(EmptySha256, result.Signature);
        Assert.True(result.IsComplete);
    }

    [Fact]
    public async Task EmptyAndMissingDirectoriesReturnTheStableEmptySignature() {
        var withoutSidecars = CreateFile("Plain.mkv");
        var missing = Path.Combine(_root.FullName, "missing", "Gone.mkv");

        var results = await new SubtitleSidecarDiscovery().DiscoverAsync(
            [withoutSidecars, missing],
            CancellationToken.None);

        Assert.Equal(2, results.Count);
        var reliableEmpty = Assert.Single(results, result => result.VideoPath == withoutSidecars);
        var unavailable = Assert.Single(results, result => result.VideoPath == missing);
        Assert.Empty(reliableEmpty.Candidates);
        Assert.Equal(EmptySha256, reliableEmpty.Signature);
        Assert.True(reliableEmpty.IsComplete);
        Assert.Empty(unavailable.Candidates);
        Assert.Equal(EmptySha256, unavailable.Signature);
        Assert.False(unavailable.IsComplete);
    }

    [Fact]
    public async Task SymlinkedSidecarsAreRejected() {
        var video = CreateFile("Linked.mkv");
        var target = CreateFile("outside.srt", "target");
        var link = Path.Combine(_root.FullName, "Linked.en.srt");
        File.CreateSymbolicLink(link, target);

        var result = Assert.Single(await new SubtitleSidecarDiscovery().DiscoverAsync(
            [video],
            CancellationToken.None));

        Assert.Empty(result.Candidates);
        Assert.Equal(EmptySha256, result.Signature);
        Assert.True(result.IsComplete);
    }

    [Fact]
    public async Task SignatureIsIndependentOfDirectoryAndCreationOrderButChangesWithFileState() {
        var firstDirectory = Directory.CreateDirectory(Path.Combine(_root.FullName, "first"));
        var secondDirectory = Directory.CreateDirectory(Path.Combine(_root.FullName, "second"));
        var firstVideo = CreateFile(firstDirectory, "Movie.mkv");
        var secondVideo = CreateFile(secondDirectory, "Movie.mkv");
        var observedAt = new DateTime(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc);

        var firstFrench = CreateFile(firstDirectory, "Movie.fr.vtt", "French");
        var firstEnglish = CreateFile(firstDirectory, "Movie.en.srt", "English");
        var secondEnglish = CreateFile(secondDirectory, "Movie.en.srt", "English");
        var secondFrench = CreateFile(secondDirectory, "Movie.fr.vtt", "French");
        foreach (var path in new[] { firstEnglish, firstFrench, secondEnglish, secondFrench }) {
            File.SetLastWriteTimeUtc(path, observedAt);
        }

        var service = new SubtitleSidecarDiscovery();
        var first = Assert.Single(await service.DiscoverAsync([firstVideo], CancellationToken.None));
        var second = Assert.Single(await service.DiscoverAsync([secondVideo], CancellationToken.None));

        Assert.Equal(first.Signature, second.Signature);
        Assert.True(first.IsComplete);
        Assert.True(second.IsComplete);
        Assert.Equal(64, first.Signature.Length);
        Assert.Equal(first.Signature.ToLowerInvariant(), first.Signature);

        await File.AppendAllTextAsync(secondEnglish, " changed");
        File.SetLastWriteTimeUtc(secondEnglish, observedAt.AddSeconds(1));
        var changed = Assert.Single(await service.DiscoverAsync([secondVideo], CancellationToken.None));

        Assert.NotEqual(second.Signature, changed.Signature);
    }

    [Fact]
    public async Task UnixSourceIdentityKeepsCanonicallyEquivalentDistinctFilenamesSeparate() {
        if (!OperatingSystem.IsLinux()) {
            return;
        }

        var video = CreateFile("Movie.mkv");
        CreateFile("Movie.caf\u00e9.srt", "composed");
        CreateFile("Movie.cafe\u0301.srt", "decomposed");

        var result = Assert.Single(await new SubtitleSidecarDiscovery().DiscoverAsync(
            [video], CancellationToken.None));

        Assert.Equal(2, result.Candidates.Count);
        Assert.Equal(2, result.Candidates.Select(candidate => candidate.SourceKey).Distinct().Count());
    }

    [Fact]
    public async Task UnixCaseDistinctVideoStemsOwnOnlyTheirMatchingSidecars() {
        if (OperatingSystem.IsWindows()) {
            return;
        }

        var upperVideo = CreateFile("Movie.mkv");
        var lowerVideo = CreateFile("movie.mkv");
        var upperSidecar = CreateFile("Movie.en.srt", "upper");
        var lowerSidecar = CreateFile("movie.fr.srt", "lower");

        var results = await new SubtitleSidecarDiscovery().DiscoverAsync(
            [upperVideo, lowerVideo], CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.Equal(
            [upperSidecar],
            Assert.Single(results, result => result.VideoPath == upperVideo)
                .Candidates.Select(candidate => candidate.Path));
        Assert.Equal(
            [lowerSidecar],
            Assert.Single(results, result => result.VideoPath == lowerVideo)
                .Candidates.Select(candidate => candidate.Path));
    }

    public void Dispose() {
        if (_root.Exists) {
            _root.Delete(recursive: true);
        }
    }

    private string CreateFile(string name, string contents = "") =>
        CreateFile(_root, name, contents);

    private static string CreateFile(DirectoryInfo directory, string name, string contents = "") {
        var path = Path.Combine(directory.FullName, name);
        File.WriteAllText(path, contents);
        return path;
    }

    private static string ExpectedSourceKey(string fileName) {
        var platformIdentity = OperatingSystem.IsWindows() ? fileName.ToUpperInvariant() : fileName;
        return Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(platformIdentity)));
    }
}

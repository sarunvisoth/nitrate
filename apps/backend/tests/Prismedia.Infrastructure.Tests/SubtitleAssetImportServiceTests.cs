using Prismedia.Contracts.Media;
using Prismedia.Application.Jobs.Ports;
using Prismedia.Infrastructure.Media.Processing;
using Prismedia.Infrastructure.Processes;

namespace Prismedia.Infrastructure.Tests;

public sealed class SubtitleAssetImportServiceTests : IDisposable {
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"prismedia-sidecar-assets-{Guid.NewGuid():N}");

    public SubtitleAssetImportServiceTests() {
        Directory.CreateDirectory(_root);
    }

    [Theory]
    [InlineData(SubtitleFormats.Ass)]
    [InlineData(SubtitleFormats.Ssa)]
    public async Task StyledImportStagesInputAndPublishesPairedRawAndVttAssets(string sourceFormat) {
        var entityId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var inputPath = Path.Combine(_root, $"Movie.en.{sourceFormat}");
        const string source = "[Script Info]\nTitle: English";
        await File.WriteAllTextAsync(inputPath, source);
        var paths = CreatePaths();
        var process = new SubtitleConversionProcessExecutor();
        var service = CreateService(paths, process);

        var result = await service.ImportAsync(
            entityId,
            inputPath,
            $"movie.en.{sourceFormat}",
            sourceFormat,
            CancellationToken.None);

        Assert.True(File.Exists(result.StoragePath));
        Assert.NotNull(result.SourcePath);
        Assert.True(File.Exists(result.SourcePath));
        Assert.Equal(source, await File.ReadAllTextAsync(result.SourcePath));
        Assert.Equal("WEBVTT\n\n00:00.000 --> 00:01.000\nCaption", await File.ReadAllTextAsync(result.StoragePath));
        Assert.Equal(
            Path.ChangeExtension(result.StoragePath, SubtitleFileExtensions.ForFormat(sourceFormat)),
            result.SourcePath);
        Assert.Equal(paths.SubtitleDir(entityId), Path.GetDirectoryName(result.StoragePath));
        Assert.Matches(@"^sidecar-[0-9a-f]{32}-[0-9a-f]{32}\.vtt$", Path.GetFileName(result.StoragePath));
        Assert.Equal(2, result.CreatedPaths.Count);
        Assert.Contains(result.StoragePath, result.CreatedPaths);
        Assert.Contains(result.SourcePath, result.CreatedPaths);

        var inputIndex = process.Arguments.Single().ToList().IndexOf("-i");
        Assert.True(inputIndex >= 0);
        var stagedInput = process.Arguments.Single()[inputIndex + 1];
        Assert.NotEqual(inputPath, stagedInput);
        Assert.StartsWith(paths.SubtitleDir(entityId), stagedInput, StringComparison.Ordinal);
        Assert.Contains("-f", process.Arguments.Single());
        Assert.Contains(SubtitleFormats.WebVttCodec, process.Arguments.Single());
        Assert.False(File.Exists(stagedInput));
        Assert.Empty(Directory.EnumerateDirectories(paths.SubtitleDir(entityId)));
    }

    [Theory]
    [InlineData(SubtitleFormats.Srt)]
    [InlineData(SubtitleFormats.Vtt)]
    public async Task PlainTextImportPublishesOnlyNormalizedVtt(string sourceFormat) {
        var entityId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var inputPath = Path.Combine(_root, $"Movie.en.{sourceFormat}");
        await File.WriteAllTextAsync(inputPath, "caption source");
        var paths = CreatePaths();
        var process = new SubtitleConversionProcessExecutor();
        var service = CreateService(paths, process);

        var result = await service.ImportAsync(
            entityId,
            inputPath,
            $"movie.en.{sourceFormat}",
            sourceFormat,
            CancellationToken.None);

        Assert.True(File.Exists(result.StoragePath));
        Assert.Null(result.SourcePath);
        Assert.Equal([result.StoragePath], result.CreatedPaths);
        Assert.Single(Directory.EnumerateFiles(paths.SubtitleDir(entityId)));
    }

    [Fact]
    public async Task ImportRejectsSymlinkWithoutInvokingFfmpeg() {
        var entityId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var targetPath = Path.Combine(_root, "outside.srt");
        var inputPath = Path.Combine(_root, "Movie.en.srt");
        await File.WriteAllTextAsync(targetPath, "outside");
        File.CreateSymbolicLink(inputPath, targetPath);
        var process = new SubtitleConversionProcessExecutor();
        var service = CreateService(CreatePaths(), process);

        await Assert.ThrowsAsync<SubtitleAssetImportException>(() => service.ImportAsync(
            entityId,
            inputPath,
            "movie.en.srt",
            SubtitleFormats.Srt,
            CancellationToken.None));

        Assert.Empty(process.Arguments);
    }

    [Fact]
    public async Task ImportRejectsSymlinkedCacheParentBeforeCreatingOrConvertingAssets() {
        var entityId = Guid.NewGuid();
        var inputPath = Path.Combine(_root, "Movie.en.srt");
        await File.WriteAllTextAsync(inputPath, "caption");
        var paths = CreatePaths();
        Directory.CreateDirectory(paths.CacheRoot);
        var outside = Path.Combine(_root, "outside-cache");
        Directory.CreateDirectory(outside);
        Directory.CreateSymbolicLink(Path.Combine(paths.CacheRoot, "videos"), outside);
        var process = new SubtitleConversionProcessExecutor();
        var service = CreateService(paths, process);

        await Assert.ThrowsAsync<SubtitleAssetImportException>(() => service.ImportAsync(
            entityId,
            inputPath,
            new string('a', 64),
            SubtitleFormats.Srt,
            CancellationToken.None));

        Assert.Empty(process.Arguments);
        Assert.Empty(Directory.EnumerateFileSystemEntries(outside));
    }

    [Fact]
    public async Task FailedConversionLeavesNoPublishedOrStagedAssets() {
        var entityId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var inputPath = Path.Combine(_root, "Movie.en.ssa");
        await File.WriteAllTextAsync(inputPath, "[Script Info]");
        var paths = CreatePaths();
        var process = new SubtitleConversionProcessExecutor(exitCode: 1, writePartialOutput: true);
        var service = CreateService(paths, process);

        await Assert.ThrowsAsync<SubtitleAssetImportException>(() => service.ImportAsync(
            entityId,
            inputPath,
            "movie.en.ssa",
            SubtitleFormats.Ssa,
            CancellationToken.None));

        var subtitleDir = paths.SubtitleDir(entityId);
        Assert.True(Directory.Exists(subtitleDir));
        Assert.Empty(Directory.EnumerateFileSystemEntries(subtitleDir));
    }

    [Fact]
    public async Task SameSourceIdentityAndBytesReuseDeterministicAssetPaths() {
        var entityId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var inputPath = Path.Combine(_root, "Movie.en.ass");
        await File.WriteAllTextAsync(inputPath, "[Script Info]\nTitle: English");
        var service = CreateService(CreatePaths(), new SubtitleConversionProcessExecutor());

        var first = await service.ImportAsync(
            entityId,
            inputPath,
            "../../unsafe/movie.en.ass",
            SubtitleFormats.Ass,
            CancellationToken.None);
        var second = await service.ImportAsync(
            entityId,
            inputPath,
            "../../unsafe/movie.en.ass",
            SubtitleFormats.Ass,
            CancellationToken.None);

        Assert.Equal(first.StoragePath, second.StoragePath);
        Assert.Equal(first.SourcePath, second.SourcePath);
        Assert.Equal(2, first.CreatedPaths.Count);
        Assert.Empty(second.CreatedPaths);
        Assert.DoesNotContain("unsafe", first.StoragePath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("..", Path.GetFileName(first.StoragePath), StringComparison.Ordinal);

        await File.WriteAllTextAsync(first.StoragePath, string.Empty);
        var repaired = await service.ImportAsync(
            entityId,
            inputPath,
            "../../unsafe/movie.en.ass",
            SubtitleFormats.Ass,
            CancellationToken.None);
        Assert.Equal(first.StoragePath, repaired.StoragePath);
        Assert.Contains(repaired.StoragePath, repaired.CreatedPaths);
        Assert.True(new FileInfo(repaired.StoragePath).Length > 0);
    }

    [Fact]
    public async Task DeleteRemovesOnlyOwnedSubtitleFilesAndIgnoresMissingPaths() {
        var entityId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var paths = CreatePaths();
        var subtitleDir = paths.SubtitleDir(entityId);
        Directory.CreateDirectory(subtitleDir);
        var ownedPath = Path.Combine(subtitleDir, "sidecar-owned.vtt");
        var outsidePath = Path.Combine(_root, "outside.vtt");
        await File.WriteAllTextAsync(ownedPath, "WEBVTT");
        await File.WriteAllTextAsync(outsidePath, "WEBVTT");
        var service = CreateService(paths, new SubtitleConversionProcessExecutor());

        await service.DeleteAsync(
            [ownedPath, outsidePath, Path.Combine(subtitleDir, "missing.vtt")],
            CancellationToken.None);

        Assert.False(File.Exists(ownedPath));
        Assert.True(File.Exists(outsidePath));
    }

    [Fact]
    public async Task DeleteRejectsLexicallyOwnedPathThroughSymlinkedCacheParent() {
        var entityId = Guid.NewGuid();
        var paths = CreatePaths();
        Directory.CreateDirectory(paths.CacheRoot);
        var outside = Path.Combine(_root, "outside-delete");
        var outsideSubtitleDir = Path.Combine(outside, entityId.ToString(), "subtitles");
        Directory.CreateDirectory(outsideSubtitleDir);
        var outsideFile = Path.Combine(outsideSubtitleDir, "victim.vtt");
        await File.WriteAllTextAsync(outsideFile, "WEBVTT");
        Directory.CreateSymbolicLink(Path.Combine(paths.CacheRoot, "videos"), outside);
        var lexicalPath = Path.Combine(
            paths.CacheRoot, "videos", entityId.ToString(), "subtitles", "victim.vtt");
        var service = CreateService(paths, new SubtitleConversionProcessExecutor());

        await service.DeleteAsync([lexicalPath], CancellationToken.None);

        Assert.True(File.Exists(outsideFile));
    }

    public void Dispose() {
        if (Directory.Exists(_root)) {
            Directory.Delete(_root, recursive: true);
        }
    }

    private AssetPathService CreatePaths() =>
        new(Path.Combine(_root, "data"), Path.Combine(_root, "cache"));

    private static SubtitleAssetImportService CreateService(
        AssetPathService paths,
        ProcessExecutor process) =>
        new(process, paths, new MediaToolOptions("/test/ffmpeg"));

    private sealed class SubtitleConversionProcessExecutor(
        int exitCode = 0,
        bool writePartialOutput = false) : ProcessExecutor {
        public List<IReadOnlyList<string>> Arguments { get; } = [];

        public override async Task<ProcessExecutionResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, string>? environment,
            CancellationToken cancellationToken,
            bool lowPriority = false) {
            Arguments.Add(arguments.ToArray());
            var outputPath = arguments[^1];
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            if (exitCode == 0 || writePartialOutput) {
                await File.WriteAllTextAsync(
                    outputPath,
                    exitCode == 0 ? "WEBVTT\n\n00:00.000 --> 00:01.000\nCaption" : "WEB",
                    cancellationToken);
            }

            return new ProcessExecutionResult(exitCode, string.Empty, exitCode == 0 ? string.Empty : "conversion failed");
        }
    }
}

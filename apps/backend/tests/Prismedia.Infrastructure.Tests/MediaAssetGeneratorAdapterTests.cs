using Prismedia.Application.Jobs.Ports;
using Prismedia.Infrastructure.Media.Adapters;
using Prismedia.Infrastructure.Media.Processing;
using Prismedia.Infrastructure.Processes;

namespace Prismedia.Infrastructure.Tests;

public sealed class MediaAssetGeneratorAdapterTests : IDisposable {
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"prismedia-subtitle-adapter-{Guid.NewGuid():N}");

    [Fact]
    public async Task EmbeddedExtractionRejectsSymlinkedSubtitleCacheParentBeforeRunningFfmpeg() {
        var cacheRoot = Path.Combine(_root, "cache");
        var outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(cacheRoot);
        Directory.CreateDirectory(outside);
        Directory.CreateSymbolicLink(Path.Combine(cacheRoot, "videos"), outside);
        var paths = new AssetPathService(Path.Combine(_root, "data"), cacheRoot);
        var process = new RecordingProcessExecutor();
        var tools = new MediaToolOptions();
        var adapter = new MediaAssetGeneratorAdapter(
            new ThumbnailService(process),
            paths,
            new SubtitleAssetImportService(process, paths, tools),
            defaultToolOptions: tools);

        await Assert.ThrowsAsync<IOException>(() => adapter.ExtractSubtitlesAsync(
            Guid.NewGuid(),
            Path.Combine(_root, "Movie.mkv"),
            [new SubtitleStreamData(2, "subrip", "en", null)],
            CancellationToken.None));

        Assert.Equal(0, process.RunCalls);
        Assert.Empty(Directory.EnumerateFileSystemEntries(outside));
    }

    [Fact]
    public async Task EmbeddedExtractionDoesNotUseUntrustedLanguageInOutputPath() {
        var cacheRoot = Path.Combine(_root, "cache");
        Directory.CreateDirectory(cacheRoot);
        var paths = new AssetPathService(Path.Combine(_root, "data"), cacheRoot);
        var process = new RecordingProcessExecutor();
        var tools = new MediaToolOptions();
        var adapter = new MediaAssetGeneratorAdapter(
            new ThumbnailService(process),
            paths,
            new SubtitleAssetImportService(process, paths, tools),
            defaultToolOptions: tools);
        var entityId = Guid.NewGuid();

        await adapter.ExtractSubtitlesAsync(
            entityId,
            Path.Combine(_root, "Movie.mkv"),
            [new SubtitleStreamData(2, "subrip", "../../escape", null)],
            CancellationToken.None);

        Assert.Equal(1, process.RunCalls);
        Assert.Equal(
            Path.Combine(paths.SubtitleDir(entityId), "embedded-2.vtt"),
            process.LastArguments[^1]);
    }

    public void Dispose() {
        if (Directory.Exists(_root)) {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class RecordingProcessExecutor : ProcessExecutor {
        public int RunCalls { get; private set; }
        public IReadOnlyList<string> LastArguments { get; private set; } = [];

        public override Task<ProcessExecutionResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, string>? environment,
            CancellationToken cancellationToken,
            bool lowPriority = false) {
            RunCalls++;
            LastArguments = arguments;
            return Task.FromResult(new ProcessExecutionResult(0, string.Empty, string.Empty));
        }
    }
}

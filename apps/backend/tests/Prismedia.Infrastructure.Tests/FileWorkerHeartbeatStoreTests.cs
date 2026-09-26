using Prismedia.Infrastructure.Health;

namespace Prismedia.Infrastructure.Tests;

public sealed class FileWorkerHeartbeatStoreTests : IDisposable {
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"prismedia-heartbeat-{Guid.NewGuid():N}");

    [Fact]
    public async Task WrittenHeartbeatReadsBack() {
        var store = new FileWorkerHeartbeatStore(_dataDir);
        var observedAt = new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

        await store.WriteAsync("worker-1", observedAt, CancellationToken.None);
        var snapshot = await store.ReadAsync(CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.Equal("worker-1", snapshot.WorkerId);
        Assert.Equal(observedAt, snapshot.ObservedAt);
    }

    [Fact]
    public async Task ReadSucceedsWhileAnotherHandleHoldsTheFileOpen() {
        var store = new FileWorkerHeartbeatStore(_dataDir);
        await store.WriteAsync("worker-1", DateTimeOffset.UtcNow, CancellationToken.None);
        var path = Path.Combine(_dataDir, "worker-heartbeat.json");

        await using var writer = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
        var snapshot = await store.ReadAsync(CancellationToken.None);

        Assert.NotNull(snapshot);
    }

    [Fact]
    public async Task FailedReplaceRemovesTheTemporaryFile() {
        var store = new FileWorkerHeartbeatStore(_dataDir);
        // A directory at the heartbeat path makes the final replace fail on every platform,
        // standing in for the Windows sharing violation raised while the API reads the file.
        Directory.CreateDirectory(Path.Combine(_dataDir, "worker-heartbeat.json"));

        await Assert.ThrowsAnyAsync<Exception>(
            () => store.WriteAsync("worker-1", DateTimeOffset.UtcNow, CancellationToken.None));

        Assert.Empty(Directory.GetFiles(_dataDir, "*.tmp"));
    }

    public void Dispose() {
        if (Directory.Exists(_dataDir)) {
            Directory.Delete(_dataDir, recursive: true);
        }
    }
}

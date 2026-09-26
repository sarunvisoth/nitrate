using System.Text.Json;
using Prismedia.Application.Health;

namespace Prismedia.Infrastructure.Health;

/// <summary>
/// File-backed worker heartbeat store shared by the API and worker processes.
/// </summary>
public sealed class FileWorkerHeartbeatStore : IWorkerHeartbeatStore {
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _path;

    /// <summary>
    /// Creates a heartbeat store under the Prismedia data directory.
    /// </summary>
    /// <param name="dataDir">Resolved Prismedia data directory shared by runtime processes.</param>
    public FileWorkerHeartbeatStore(string dataDir) {
        _path = Path.Combine(dataDir, "worker-heartbeat.json");
    }

    /// <inheritdoc />
    public async Task<WorkerHeartbeatSnapshot?> ReadAsync(CancellationToken cancellationToken) {
        try {
            if (!File.Exists(_path)) {
                return null;
            }

            // Share write and delete so the worker's atomic replace is not blocked by this read on
            // Windows, where a plain OpenRead makes File.Move(..., overwrite: true) fail.
            await using var stream = new FileStream(
                _path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            var payload = await JsonSerializer.DeserializeAsync<HeartbeatPayload>(
                stream,
                JsonOptions,
                cancellationToken);

            return payload is null || string.IsNullOrWhiteSpace(payload.WorkerId)
                ? null
                : new WorkerHeartbeatSnapshot(payload.WorkerId, payload.ObservedAt);
        } catch (JsonException) {
            return null;
        } catch (IOException) {
            return null;
        } catch (UnauthorizedAccessException) {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task WriteAsync(
        string workerId,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken) {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);

        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{_path}.{Guid.NewGuid():N}.tmp";
        await using (var stream = File.Open(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
            await JsonSerializer.SerializeAsync(
                stream,
                new HeartbeatPayload(workerId, observedAt),
                JsonOptions,
                cancellationToken);
        }

        try {
            File.Move(tempPath, _path, overwrite: true);
        } catch {
            TryDelete(tempPath);
            throw;
        }
    }

    private static void TryDelete(string path) {
        try {
            File.Delete(path);
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
        }
    }

    private sealed record HeartbeatPayload(string WorkerId, DateTimeOffset ObservedAt);
}

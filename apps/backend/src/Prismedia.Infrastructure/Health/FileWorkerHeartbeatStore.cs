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

            await using var stream = File.OpenRead(_path);
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

        File.Move(tempPath, _path, overwrite: true);
    }

    private sealed record HeartbeatPayload(string WorkerId, DateTimeOffset ObservedAt);
}

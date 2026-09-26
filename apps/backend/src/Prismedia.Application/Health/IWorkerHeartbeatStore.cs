namespace Prismedia.Application.Health;

/// <summary>
/// Application port for publishing and reading the worker process heartbeat.
/// </summary>
public interface IWorkerHeartbeatStore {
    /// <summary>
    /// Reads the most recent worker heartbeat, if one has been published.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the read operation.</param>
    /// <returns>The most recent heartbeat, or <c>null</c> when no heartbeat exists.</returns>
    Task<WorkerHeartbeatSnapshot?> ReadAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Publishes a heartbeat for the running worker process.
    /// </summary>
    /// <param name="workerId">Stable identifier for the current worker process lifetime.</param>
    /// <param name="observedAt">UTC time when the worker was observed alive.</param>
    /// <param name="cancellationToken">Token used to cancel the write operation.</param>
    Task WriteAsync(string workerId, DateTimeOffset observedAt, CancellationToken cancellationToken);
}

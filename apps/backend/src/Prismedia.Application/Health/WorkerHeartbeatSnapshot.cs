namespace Prismedia.Application.Health;

/// <summary>
/// Most recent worker heartbeat observed by the API.
/// </summary>
/// <param name="WorkerId">Stable identifier for the worker process lifetime.</param>
/// <param name="ObservedAt">UTC time when the worker last published a heartbeat.</param>
public sealed record WorkerHeartbeatSnapshot(string WorkerId, DateTimeOffset ObservedAt);

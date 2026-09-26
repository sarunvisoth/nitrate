namespace Prismedia.Contracts.System;

/// <summary>
/// Health response for checking whether the background worker is publishing heartbeats.
/// </summary>
/// <param name="Status">Worker status code: "online" when the heartbeat is fresh, otherwise "offline".</param>
/// <param name="WorkerId">Identifier for the worker process that published the heartbeat.</param>
/// <param name="LastSeenAt">UTC time when the worker last published a heartbeat.</param>
/// <param name="StaleAfterSeconds">Number of seconds after which a heartbeat is treated as offline.</param>
public sealed record WorkerHealthResponse(
    string Status,
    string? WorkerId,
    DateTimeOffset? LastSeenAt,
    int StaleAfterSeconds);

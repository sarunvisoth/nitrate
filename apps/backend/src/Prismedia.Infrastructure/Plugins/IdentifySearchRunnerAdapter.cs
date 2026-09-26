using Prismedia.Application.Jobs.Ports;

namespace Prismedia.Infrastructure.Plugins;

/// <summary>
/// Adapts <see cref="IdentifyQueueService"/> for use by the identify-search job handler. Each call
/// runs one requested provider search and persists the outcome onto the queue item.
/// </summary>
internal sealed class IdentifySearchRunnerAdapter(IdentifyQueueService queueService) : IIdentifySearchRunner {
    public Task RunAsync(IdentifySearchPayload payload, Guid searchJobId, bool isFinalAttempt, CancellationToken cancellationToken) =>
        queueService.RunSearchAsync(payload, searchJobId, isFinalAttempt, cancellationToken);
}

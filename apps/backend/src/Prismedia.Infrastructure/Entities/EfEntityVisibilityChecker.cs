using Prismedia.Application.Entities;

namespace Prismedia.Infrastructure.Entities;

/// <summary>
/// <see cref="IEntityVisibilityChecker"/> backed by the read service's library-visibility
/// machinery, so guards and list filtering can never drift apart.
/// </summary>
public sealed class EfEntityVisibilityChecker(EfEntityReadService readService) : IEntityVisibilityChecker {
    /// <inheritdoc />
    public Task<bool> IsVisibleAsync(Guid entityId, CancellationToken cancellationToken) =>
        readService.IsEntityVisibleToCurrentUserAsync(entityId, cancellationToken);
}

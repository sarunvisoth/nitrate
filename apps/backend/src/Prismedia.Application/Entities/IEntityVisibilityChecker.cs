namespace Prismedia.Application.Entities;

/// <summary>
/// Application port answering whether the current user may see an entity at all
/// (library-access and disabled-root rules). Mutation and streaming paths check this
/// up front so hidden entities behave as missing (404) rather than forbidden.
/// </summary>
public interface IEntityVisibilityChecker {
    /// <summary>True when the entity exists and is visible to the current user.</summary>
    Task<bool> IsVisibleAsync(Guid entityId, CancellationToken cancellationToken);
}

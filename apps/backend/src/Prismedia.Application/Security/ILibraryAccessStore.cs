namespace Prismedia.Application.Security;

/// <summary>
/// Read/write port for per-user library access grants, extending the read side used by
/// the current-user context. Admins are never granted rows — they bypass the table.
/// Implementations enforce the NSFW wall on every write: a user whose account blocks NSFW
/// content can never hold a grant to an NSFW library, regardless of the caller.
/// </summary>
public interface ILibraryAccessStore : ILibraryAccessReader {
    /// <summary>User ids granted to each library root (roots with no grants are absent).</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetAccessByRootAsync(CancellationToken cancellationToken);

    /// <summary>Library root ids granted to each user (users with no grants are absent).</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetAccessByUserAsync(CancellationToken cancellationToken);

    /// <summary>Replaces the set of users granted to one library root (NSFW-blocked users are silently excluded for an NSFW root).</summary>
    Task ReplaceRootAccessAsync(Guid libraryRootId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);

    /// <summary>Replaces the set of library roots granted to one user (NSFW roots are silently excluded for an NSFW-blocked user).</summary>
    Task ReplaceUserAccessAsync(Guid userId, IReadOnlyCollection<Guid> libraryRootIds, CancellationToken cancellationToken);

    /// <summary>Adds grants for one library root without disturbing existing ones (NSFW-blocked users are silently excluded for an NSFW root).</summary>
    Task GrantRootAccessAsync(Guid libraryRootId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);

    /// <summary>Removes a user's grants to NSFW libraries — the cleanup half of blocking their NSFW permission.</summary>
    Task RevokeNsfwAccessAsync(Guid userId, CancellationToken cancellationToken);
}

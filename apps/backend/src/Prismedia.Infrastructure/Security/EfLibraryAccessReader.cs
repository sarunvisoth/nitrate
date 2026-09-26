using Microsoft.EntityFrameworkCore;
using Prismedia.Application.Security;
using Prismedia.Infrastructure.Persistence;
using Prismedia.Infrastructure.Persistence.Entities;

namespace Prismedia.Infrastructure.Security;

/// <summary>
/// EF-backed store for per-user library access grants. Every write path enforces the NSFW wall
/// centrally: a user whose account blocks NSFW content can never hold a grant to an NSFW library,
/// no matter which caller (users admin, library access editor, library creation) asked for it.
/// </summary>
public sealed class EfLibraryAccessReader : ILibraryAccessStore {
    private readonly PrismediaDbContext _db;

    public EfLibraryAccessReader(PrismediaDbContext db) {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlySet<Guid>> GetAllowedRootIdsAsync(Guid userId, CancellationToken cancellationToken) =>
        (await _db.UserLibraryAccess.AsNoTracking()
            .Where(row => row.UserId == userId)
            .Select(row => row.LibraryRootId)
            .ToArrayAsync(cancellationToken))
        .ToHashSet();

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetAccessByRootAsync(CancellationToken cancellationToken) =>
        (await _db.UserLibraryAccess.AsNoTracking().ToArrayAsync(cancellationToken))
        .GroupBy(row => row.LibraryRootId)
        .ToDictionary(
            group => group.Key,
            group => (IReadOnlyList<Guid>)group.Select(row => row.UserId).ToArray());

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetAccessByUserAsync(CancellationToken cancellationToken) =>
        (await _db.UserLibraryAccess.AsNoTracking().ToArrayAsync(cancellationToken))
        .GroupBy(row => row.UserId)
        .ToDictionary(
            group => group.Key,
            group => (IReadOnlyList<Guid>)group.Select(row => row.LibraryRootId).ToArray());

    /// <inheritdoc />
    public async Task ReplaceRootAccessAsync(
        Guid libraryRootId,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken) {
        var allowedUserIds = await FilterNsfwCapableUsersAsync(libraryRootId, userIds, cancellationToken);
        var existing = await _db.UserLibraryAccess
            .Where(row => row.LibraryRootId == libraryRootId)
            .ToArrayAsync(cancellationToken);
        _db.UserLibraryAccess.RemoveRange(existing.Where(row => !allowedUserIds.Contains(row.UserId)));
        AddMissing(allowedUserIds.Except(existing.Select(row => row.UserId)), userId => (userId, libraryRootId));
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task ReplaceUserAccessAsync(
        Guid userId,
        IReadOnlyCollection<Guid> libraryRootIds,
        CancellationToken cancellationToken) {
        var allowedRootIds = await FilterRootsForUserAsync(userId, libraryRootIds, cancellationToken);
        var existing = await _db.UserLibraryAccess
            .Where(row => row.UserId == userId)
            .ToArrayAsync(cancellationToken);
        _db.UserLibraryAccess.RemoveRange(existing.Where(row => !allowedRootIds.Contains(row.LibraryRootId)));
        AddMissing(allowedRootIds.Except(existing.Select(row => row.LibraryRootId)), rootId => (userId, rootId));
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task GrantRootAccessAsync(
        Guid libraryRootId,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken) {
        var allowedUserIds = await FilterNsfwCapableUsersAsync(libraryRootId, userIds, cancellationToken);
        var existing = await _db.UserLibraryAccess.AsNoTracking()
            .Where(row => row.LibraryRootId == libraryRootId)
            .Select(row => row.UserId)
            .ToArrayAsync(cancellationToken);
        AddMissing(allowedUserIds.Except(existing), userId => (userId, libraryRootId));
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task RevokeNsfwAccessAsync(Guid userId, CancellationToken cancellationToken) {
        var nsfwGrants = await (
            from access in _db.UserLibraryAccess
            join root in _db.LibraryRoots.AsNoTracking() on access.LibraryRootId equals root.Id
            where access.UserId == userId && root.IsNsfw
            select access)
            .ToArrayAsync(cancellationToken);
        if (nsfwGrants.Length == 0) {
            return;
        }

        _db.UserLibraryAccess.RemoveRange(nsfwGrants);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>For an NSFW library, only users whose account allows NSFW may hold a grant; SFW libraries pass everyone.</summary>
    private async Task<IReadOnlyCollection<Guid>> FilterNsfwCapableUsersAsync(
        Guid libraryRootId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) {
        if (userIds.Count == 0) {
            return userIds;
        }

        var isNsfwRoot = await _db.LibraryRoots.AsNoTracking()
            .Where(row => row.Id == libraryRootId)
            .Select(row => row.IsNsfw)
            .FirstOrDefaultAsync(cancellationToken);
        if (!isNsfwRoot) {
            return userIds;
        }

        return await _db.Users.AsNoTracking()
            .Where(row => userIds.Contains(row.Id) && row.AllowNsfw)
            .Select(row => row.Id)
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>For an NSFW-blocked user, NSFW libraries are silently excluded from the requested set.</summary>
    private async Task<IReadOnlyCollection<Guid>> FilterRootsForUserAsync(
        Guid userId, IReadOnlyCollection<Guid> libraryRootIds, CancellationToken cancellationToken) {
        if (libraryRootIds.Count == 0) {
            return libraryRootIds;
        }

        var allowNsfw = await _db.Users.AsNoTracking()
            .Where(row => row.Id == userId)
            .Select(row => row.AllowNsfw)
            .FirstOrDefaultAsync(cancellationToken);
        if (allowNsfw) {
            return libraryRootIds;
        }

        return await _db.LibraryRoots.AsNoTracking()
            .Where(row => libraryRootIds.Contains(row.Id) && !row.IsNsfw)
            .Select(row => row.Id)
            .ToArrayAsync(cancellationToken);
    }

    private void AddMissing(IEnumerable<Guid> ids, Func<Guid, (Guid UserId, Guid RootId)> map) {
        var now = DateTimeOffset.UtcNow;
        foreach (var id in ids) {
            var (userId, rootId) = map(id);
            _db.UserLibraryAccess.Add(new UserLibraryAccessRow {
                UserId = userId,
                LibraryRootId = rootId,
                CreatedAt = now
            });
        }
    }
}

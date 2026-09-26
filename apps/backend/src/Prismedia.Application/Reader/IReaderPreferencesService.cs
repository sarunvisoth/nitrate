using Prismedia.Contracts.Reader;

namespace Prismedia.Application.Reader;

/// <summary>
/// Reads and writes the current user's reader preferences (typography, theme, flow, translation
/// default) with last-write-wins semantics. One row per user; reads return documented defaults
/// when nothing has been saved yet.
/// </summary>
public interface IReaderPreferencesService {
    /// <summary>Gets the current user's saved preferences, or defaults when there is no user or no row.</summary>
    Task<ReaderPreferencesResponse> GetAsync(CancellationToken cancellationToken);

    /// <summary>Creates or replaces the current user's preferences, applied only if not older than stored.</summary>
    Task<ReaderPreferencesWriteResult> UpsertAsync(
        UpsertReaderPreferencesRequest request,
        CancellationToken cancellationToken);
}

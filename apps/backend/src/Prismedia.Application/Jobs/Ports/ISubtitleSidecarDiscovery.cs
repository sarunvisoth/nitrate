namespace Prismedia.Application.Jobs.Ports;

/// <summary>
/// Discovers adjacent subtitle files for a batch of source videos without opening their content.
/// </summary>
public interface ISubtitleSidecarDiscovery {
    /// <summary>
    /// Returns one result for every requested video path. Videos with no readable sidecars retain
    /// an empty candidate list and a stable non-null signature.
    /// </summary>
    /// <param name="videoPaths">Source-video paths to inspect, normally grouped from one scan.</param>
    /// <param name="cancellationToken">Token used to cancel directory enumeration.</param>
    Task<IReadOnlyList<VideoSubtitleSidecarDiscovery>> DiscoverAsync(
        IReadOnlyCollection<string> videoPaths,
        CancellationToken cancellationToken);
}

/// <summary>Adjacent subtitle files and their deterministic set signature for one video.</summary>
/// <param name="VideoPath">Absolute source-video path supplied to discovery.</param>
/// <param name="Candidates">Safe ordinary-file candidates owned by this video filename.</param>
/// <param name="Signature">Lowercase SHA-256 of sorted candidate identity, size, and mtime.</param>
/// <param name="IsComplete">
/// Whether directory enumeration and relevant file metadata reads completed reliably. Callers must
/// not reconcile an incomplete result as though it represented the current sidecar set.
/// </param>
public sealed record VideoSubtitleSidecarDiscovery(
    string VideoPath,
    IReadOnlyList<SubtitleSidecarCandidate> Candidates,
    string Signature,
    bool IsComplete);

/// <summary>A filesystem subtitle sidecar observed safely during discovery.</summary>
/// <param name="Path">Absolute path to the sidecar; callers must revalidate before opening it.</param>
/// <param name="SourceKey">
/// Stable lowercase SHA-256 identity of the platform-normalized filename within the video's directory.
/// </param>
/// <param name="Format">Canonical lowercase subtitle format.</param>
/// <param name="Language">Canonical culture code or the undetermined-language code.</param>
/// <param name="Label">Humanized non-language filename tags, when present.</param>
/// <param name="SizeBytes">Observed file length.</param>
/// <param name="ModifiedTicks">Observed UTC last-write timestamp in ticks.</param>
public sealed record SubtitleSidecarCandidate(
    string Path,
    string SourceKey,
    string Format,
    string Language,
    string? Label,
    long SizeBytes,
    long ModifiedTicks);

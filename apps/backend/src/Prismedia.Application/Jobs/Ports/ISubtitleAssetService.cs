namespace Prismedia.Application.Jobs.Ports;

/// <summary>Materializes and cleans app-owned subtitle playback assets.</summary>
public interface ISubtitleAssetService {
    /// <summary>Extracts embedded text streams into normalized WebVTT files.</summary>
    Task<IReadOnlyList<string>> ExtractSubtitlesAsync(
        Guid entityId,
        string inputPath,
        IReadOnlyList<SubtitleStreamData> streams,
        CancellationToken cancellationToken);

    /// <summary>
    /// Copies one discovered sidecar into app-owned staging, normalizes it to WebVTT, and
    /// atomically publishes content-versioned cache assets. Styled ASS/SSA source is preserved.
    /// </summary>
    Task<ImportedSidecarSubtitleAssets> ImportSidecarSubtitleAsync(
        Guid entityId,
        string inputPath,
        string sourceKey,
        string sourceFormat,
        CancellationToken cancellationToken);

    /// <summary>Best-effort removal of exact app-owned subtitle paths after reconciliation.</summary>
    Task DeleteSubtitleAssetsAsync(
        IReadOnlyCollection<string> paths,
        CancellationToken cancellationToken);

}

/// <summary>App-owned files published for one imported subtitle sidecar.</summary>
/// <param name="StoragePath">Absolute normalized WebVTT cache path.</param>
/// <param name="SourcePath">Absolute preserved ASS/SSA cache path, or null for SRT/VTT.</param>
/// <param name="CreatedPaths">Paths newly published by this call and safe to remove before commit.</param>
public sealed record ImportedSidecarSubtitleAssets(
    string StoragePath,
    string? SourcePath,
    IReadOnlyList<string> CreatedPaths);

/// <summary>Raised when a subtitle sidecar cannot be imported safely and completely.</summary>
public sealed class SubtitleAssetImportException : Exception {
    public SubtitleAssetImportException(string message) : base(message) { }

    public SubtitleAssetImportException(string message, Exception innerException)
        : base(message, innerException) { }
}

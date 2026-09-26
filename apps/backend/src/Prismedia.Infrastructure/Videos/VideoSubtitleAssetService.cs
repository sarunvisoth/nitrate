using Microsoft.EntityFrameworkCore;
using Prismedia.Application.Videos;
using Prismedia.Contracts.Media;
using Prismedia.Domain.Entities;
using Prismedia.Infrastructure.Media.Processing;
using Prismedia.Infrastructure.Persistence;
using Prismedia.Infrastructure.Processes;

namespace Prismedia.Infrastructure.Videos;

/// <summary>
/// EF-backed subtitle asset resolver for video subtitle tracks.
/// </summary>
public sealed class VideoSubtitleAssetService : IVideoSubtitleAssetService {
    private readonly PrismediaDbContext _db;
    private readonly ProcessExecutor _processExecutor;
    private readonly MediaToolOptions _mediaTools;
    private readonly AssetPathService _assetPaths;

    /// <summary>
    /// Creates a subtitle asset resolver over the database context.
    /// </summary>
    /// <param name="db">Database context used to find subtitle rows.</param>
    /// <param name="processExecutor">Process runner used to extract embedded styled subtitles on demand.</param>
    /// <param name="mediaTools">Configured ffmpeg and ffprobe executable paths.</param>
    /// <param name="assetPaths">Generated-asset paths used to contain sidecar reads to app-owned cache.</param>
    public VideoSubtitleAssetService(
        PrismediaDbContext db,
        ProcessExecutor processExecutor,
        MediaToolOptions mediaTools,
        AssetPathService assetPaths) {
        _db = db;
        _processExecutor = processExecutor;
        _mediaTools = mediaTools;
        _assetPaths = assetPaths;
    }

    /// <summary>
    /// Finds the normalized WebVTT subtitle file for one subtitle track.
    /// </summary>
    /// <param name="videoId">Video entity identifier that owns the track.</param>
    /// <param name="trackId">Subtitle track identifier.</param>
    /// <param name="cancellationToken">Token used to cancel the lookup.</param>
    /// <returns>Subtitle asset metadata, or null when the track or file is unavailable.</returns>
    public async Task<VideoSubtitleAsset?> GetSubtitleAsync(
        Guid videoId,
        Guid trackId,
        CancellationToken cancellationToken) {
        var row = await _db.EntitySubtitles
            .AsNoTracking()
            .Where(row => row.EntityId == videoId && row.Id == trackId)
            .Select(row => new { row.StoragePath, row.Source })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null) {
            return null;
        }

        return row.Source == EntitySubtitleSource.Sidecar
            ? ExistingOwnedSidecarAsset(videoId, row.StoragePath, MediaContentTypes.VttUtf8)
            : ExistingAsset(row.StoragePath, MediaContentTypes.VttUtf8);
    }

    /// <summary>
    /// Finds the original ASS/SSA subtitle source for one subtitle track, when preserved.
    /// </summary>
    /// <param name="videoId">Video entity identifier that owns the track.</param>
    /// <param name="trackId">Subtitle track identifier.</param>
    /// <param name="cancellationToken">Token used to cancel the lookup.</param>
    /// <returns>Subtitle source metadata, or null when no raw source is available.</returns>
    public async Task<VideoSubtitleAsset?> GetSubtitleSourceAsync(
        Guid videoId,
        Guid trackId,
        CancellationToken cancellationToken) {
        var row = await _db.EntitySubtitles
            .AsNoTracking()
            .Where(subtitle => subtitle.EntityId == videoId && subtitle.Id == trackId)
            .Select(subtitle => new {
                subtitle.StoragePath,
                subtitle.SourcePath,
                subtitle.SourceFormat,
                subtitle.Source
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null ||
            string.IsNullOrWhiteSpace(row.SourcePath) ||
            !SubtitleFormats.IsStyled(row.SourceFormat)) {
            return null;
        }

        if (row.Source == EntitySubtitleSource.Sidecar) {
            return ExistingPairedSidecarSource(
                videoId,
                row.StoragePath,
                row.SourcePath,
                row.SourceFormat);
        }

        var preservedAsset = ExistingAsset(row.SourcePath, MediaContentTypes.SsaUtf8);
        if (preservedAsset is not null) {
            return preservedAsset;
        }

        if (row.Source != EntitySubtitleSource.Embedded ||
            !int.TryParse(row.SourcePath, out var streamIndex)) {
            return null;
        }

        var sourcePath = await _db.EntityFiles
            .AsNoTracking()
            .Where(file => file.EntityId == videoId && file.Role == EntityFileRole.Source)
            .Select(file => file.Path)
            .SingleOrDefaultAsync(cancellationToken);

        if (!ExistingSource(sourcePath)) {
            return null;
        }

        var outputPath = EmbeddedSubtitleSourcePath(row.StoragePath, row.SourceFormat);
        try {
            _assetPaths.EnsureSubtitleDirectorySafe(videoId);
        } catch (IOException) {
            return null;
        } catch (UnauthorizedAccessException) {
            return null;
        }
        if (!_assetPaths.IsSubtitleAssetPath(videoId, outputPath)) {
            return null;
        }
        if (SubtitleAssetAvailability.IsOrdinaryNonEmptyFile(outputPath)) {
            return new VideoSubtitleAsset(outputPath, MediaContentTypes.SsaUtf8);
        }

        var extracted = await ExtractEmbeddedStyledSubtitleAsync(
            sourcePath!,
            streamIndex,
            outputPath,
            cancellationToken);

        return extracted ? new VideoSubtitleAsset(outputPath, MediaContentTypes.SsaUtf8) : null;
    }

    private static VideoSubtitleAsset? ExistingAsset(string? path, string contentType) {
        if (!SubtitleAssetAvailability.IsOrdinaryNonEmptyFile(path)) {
            return null;
        }

        return new VideoSubtitleAsset(path!, contentType);
    }

    private async Task<bool> ExtractEmbeddedStyledSubtitleAsync(
        string sourcePath,
        int streamIndex,
        string outputPath,
        CancellationToken cancellationToken) {
        var extension = Path.GetExtension(outputPath);
        var stagingPath = Path.Combine(
            Path.GetDirectoryName(outputPath)!,
            $".embedded-source-{Guid.NewGuid():N}{extension}");
        try {
            var result = await _processExecutor.RunAsync(
                _mediaTools.FfmpegPath,
                [
                    "-y",
                    "-v", "error",
                    "-i", sourcePath,
                    "-map", $"0:{streamIndex}",
                    "-c:s", "copy",
                    stagingPath
                ],
                null,
                cancellationToken,
                lowPriority: true);

            if (result.ExitCode != 0 || !SubtitleAssetAvailability.IsOrdinaryNonEmptyFile(stagingPath)) {
                return false;
            }

            File.Move(stagingPath, outputPath, overwrite: true);
            return SubtitleAssetAvailability.IsOrdinaryNonEmptyFile(outputPath);
        } finally {
            TryDeleteFile(stagingPath);
        }
    }

    private static string EmbeddedSubtitleSourcePath(string storagePath, string sourceFormat) {
        return Path.ChangeExtension(storagePath, SubtitleFileExtensions.ForFormat(sourceFormat));
    }

    private static bool ExistingSource(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        Path.IsPathRooted(path) &&
        File.Exists(path);

    private VideoSubtitleAsset? ExistingPairedSidecarSource(
        Guid videoId,
        string storagePath,
        string sourcePath,
        string sourceFormat) {
        return SubtitleAssetAvailability.IsStyledSidecarPairAvailable(
            _assetPaths, videoId, storagePath, sourcePath, sourceFormat)
            ? new VideoSubtitleAsset(sourcePath, MediaContentTypes.SsaUtf8)
            : null;
    }

    private VideoSubtitleAsset? ExistingOwnedSidecarAsset(
        Guid videoId,
        string path,
        string contentType) =>
        SubtitleAssetAvailability.IsOwnedOrdinaryFile(_assetPaths, videoId, path)
            ? new VideoSubtitleAsset(path, contentType)
            : null;

    private static void TryDeleteFile(string path) {
        try {
            if (File.Exists(path)) {
                File.Delete(path);
            }
        } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            // Failed on-demand extraction never publishes or references its staging file.
        }
    }
}

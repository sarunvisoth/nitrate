using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using Prismedia.Application.Files;
using Prismedia.Application.Jobs.Ports;
using Prismedia.Contracts.Media;
using Prismedia.Infrastructure.Processes;

namespace Prismedia.Infrastructure.Media.Processing;

/// <summary>
/// Imports untrusted adjacent subtitle files into app-owned, content-versioned cache assets.
/// Source files are copied into private staging before ffmpeg sees them, and completed files
/// are published with same-filesystem renames so readers never observe partial output.
/// </summary>
public sealed class SubtitleAssetImportService(
    ProcessExecutor processExecutor,
    AssetPathService paths,
    MediaToolOptions defaultToolOptions) {

    /// <summary>Safely imports and normalizes one supported subtitle sidecar.</summary>
    public async Task<ImportedSidecarSubtitleAssets> ImportAsync(
        Guid entityId,
        string inputPath,
        string sourceKey,
        string sourceFormat,
        CancellationToken cancellationToken,
        MediaToolOptions? toolOptions = null) {
        var format = NormalizeFormat(sourceFormat);
        var extension = SubtitleFileExtensions.ForFormat(format);
        var sourcePath = NormalizeAndValidateSourcePath(inputPath, extension);
        if (string.IsNullOrWhiteSpace(sourceKey)) {
            throw new SubtitleAssetImportException("Subtitle source identity is required.");
        }

        var stagingDirectory = paths.SubtitleStagingDir(entityId, Guid.NewGuid());
        try {
            EnsureSafeOutputDirectory(entityId);
            Directory.CreateDirectory(stagingDirectory);
            EnsureOrdinaryDirectory(stagingDirectory);

            var stagedSourcePath = Path.Combine(stagingDirectory, "source" + extension);
            var contentToken = await CopySourceToStagingAsync(
                sourcePath,
                stagedSourcePath,
                cancellationToken);
            var sourceToken = HashToken(sourceKey);
            var finalPaths = paths.SidecarSubtitlePaths(entityId, sourceToken, contentToken, format);

            var stagedStoragePath = Path.Combine(stagingDirectory, "normalized" + SubtitleFileExtensions.Vtt);
            await ConvertToWebVttAsync(
                stagedSourcePath,
                stagedStoragePath,
                cancellationToken,
                toolOptions ?? defaultToolOptions);

            return PublishAssets(stagedSourcePath, stagedStoragePath, finalPaths);
        } catch (OperationCanceledException) {
            throw;
        } catch (SubtitleAssetImportException) {
            throw;
        } catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) {
            throw new SubtitleAssetImportException("The subtitle sidecar could not be imported safely.", exception);
        } finally {
            TryDeleteDirectory(stagingDirectory);
        }
    }

    /// <summary>Best-effort exact cleanup for generated subtitle assets.</summary>
    public Task DeleteAsync(
        IReadOnlyCollection<string> assetPaths,
        CancellationToken cancellationToken) {
        foreach (var path in assetPaths.Distinct(FileSystemPathComparison.Comparer)) {
            cancellationToken.ThrowIfCancellationRequested();
            if (!paths.IsSubtitleAssetPath(path)) {
                continue;
            }

            try {
                if (!paths.IsSubtitleAssetPath(path)) {
                    continue;
                }
                if (File.Exists(path) || IsReparsePoint(path)) {
                    File.Delete(path);
                }
            } catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) {
                // Reconciliation cleanup is intentionally best effort. A later entity or maintenance
                // cleanup removes any generation that remains under the owned video cache directory.
            }
        }

        return Task.CompletedTask;
    }

    private static string NormalizeFormat(string format) {
        if (string.IsNullOrWhiteSpace(format) || !SubtitleFormats.IsSupportedSidecar(format)) {
            throw new SubtitleAssetImportException("The subtitle sidecar format is unsupported.");
        }

        return format.Trim().ToLowerInvariant();
    }

    private static string NormalizeAndValidateSourcePath(string inputPath, string expectedExtension) {
        if (string.IsNullOrWhiteSpace(inputPath) || !Path.IsPathRooted(inputPath)) {
            throw new SubtitleAssetImportException("The subtitle sidecar path must be absolute.");
        }

        string fullPath;
        try {
            fullPath = Path.GetFullPath(inputPath);
        } catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException) {
            throw new SubtitleAssetImportException("The subtitle sidecar path is invalid.", exception);
        }

        if (!string.Equals(Path.GetExtension(fullPath), expectedExtension, StringComparison.OrdinalIgnoreCase)) {
            throw new SubtitleAssetImportException("The subtitle sidecar extension does not match its format.");
        }

        EnsureOrdinaryFile(fullPath);
        return fullPath;
    }

    private async Task<string> CopySourceToStagingAsync(
        string sourcePath,
        string stagingPath,
        CancellationToken cancellationToken) {
        EnsureOrdinaryFile(sourcePath);
        var before = SnapshotSource(sourcePath);
        await using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        // Re-check after the handle is open so a link swapped in between discovery and import is
        // rejected before its bytes can be handed to ffmpeg.
        EnsureOrdinaryFile(sourcePath);
        if (source.Length != before.Length) {
            throw new SubtitleAssetImportException("The subtitle sidecar changed while it was being opened.");
        }

        await using var destination = new FileStream(
            stagingPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try {
            int read;
            while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0) {
                hash.AppendData(buffer, 0, read);
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            await destination.FlushAsync(cancellationToken);
        } finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        EnsureOrdinaryFile(sourcePath);
        var after = SnapshotSource(sourcePath);
        if (before != after || source.Length != destination.Length) {
            throw new SubtitleAssetImportException("The subtitle sidecar changed while it was being copied.");
        }

        return HexToken(hash.GetHashAndReset());
    }

    private async Task ConvertToWebVttAsync(
        string stagedSourcePath,
        string stagedStoragePath,
        CancellationToken cancellationToken,
        MediaToolOptions toolOptions) {
        var result = await processExecutor.RunAsync(
            toolOptions.FfmpegPath,
            [
                "-y",
                "-v", "error",
                "-i", stagedSourcePath,
                "-map", "0:0",
                "-c:s", SubtitleFormats.WebVttCodec,
                "-f", SubtitleFormats.WebVttCodec,
                stagedStoragePath,
            ],
            null,
            cancellationToken,
            lowPriority: true);

        if (result.ExitCode != 0 || !IsNonEmptyOrdinaryFile(stagedStoragePath)) {
            throw new SubtitleAssetImportException("ffmpeg could not normalize the subtitle sidecar to WebVTT.");
        }
    }

    private ImportedSidecarSubtitleAssets PublishAssets(
        string stagedSourcePath,
        string stagedStoragePath,
        SubtitleAssetPaths finalPaths) {
        var createdPaths = new List<string>();
        try {
            if (finalPaths.SourcePath is not null &&
                PublishIfMissing(stagedSourcePath, finalPaths.SourcePath)) {
                createdPaths.Add(finalPaths.SourcePath);
            }

            if (PublishIfMissing(stagedStoragePath, finalPaths.StoragePath)) {
                createdPaths.Add(finalPaths.StoragePath);
            }

            if (!IsNonEmptyOrdinaryFile(finalPaths.StoragePath) ||
                (finalPaths.SourcePath is not null && !IsNonEmptyOrdinaryFile(finalPaths.SourcePath))) {
                throw new SubtitleAssetImportException("The completed subtitle cache assets are unavailable.");
            }

            return new ImportedSidecarSubtitleAssets(
                finalPaths.StoragePath,
                finalPaths.SourcePath,
                createdPaths.ToArray());
        } catch {
            foreach (var createdPath in createdPaths) {
                TryDeleteFile(createdPath);
            }

            throw;
        }
    }

    private static bool PublishIfMissing(string stagingPath, string finalPath) {
        if (File.Exists(finalPath) || IsReparsePoint(finalPath)) {
            if (IsNonEmptyOrdinaryFile(finalPath)) {
                File.Delete(stagingPath);
                return false;
            }

            // A prior crash cannot leave a partial file because publication is a rename, but an
            // externally corrupted cache entry or planted link must not permanently poison this
            // content-addressed generation. Removing the exact app-owned leaf is safe.
            File.Delete(finalPath);
        }

        try {
            File.Move(stagingPath, finalPath);
            return true;
        } catch (IOException) when (File.Exists(finalPath)) {
            // Another importer published the same content-addressed generation concurrently.
            File.Delete(stagingPath);
            return false;
        }
    }

    private void EnsureSafeOutputDirectory(Guid entityId) {
        paths.EnsureSubtitleDirectorySafe(entityId);
    }

    private static void EnsureOrdinaryFile(string path) {
        FileAttributes attributes;
        try {
            attributes = File.GetAttributes(path);
        } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            throw new SubtitleAssetImportException("The subtitle sidecar is missing or unreadable.", exception);
        }

        if (attributes.HasFlag(FileAttributes.Directory) ||
            attributes.HasFlag(FileAttributes.ReparsePoint)) {
            throw new SubtitleAssetImportException("The subtitle sidecar must be an ordinary file, not a link.");
        }
    }

    private static void EnsureOrdinaryDirectory(string path) {
        var attributes = File.GetAttributes(path);
        if (!attributes.HasFlag(FileAttributes.Directory) ||
            attributes.HasFlag(FileAttributes.ReparsePoint)) {
            throw new SubtitleAssetImportException("Subtitle cache paths cannot traverse filesystem links.");
        }
    }

    private static bool IsNonEmptyOrdinaryFile(string path) {
        try {
            var attributes = File.GetAttributes(path);
            return !attributes.HasFlag(FileAttributes.Directory) &&
                !attributes.HasFlag(FileAttributes.ReparsePoint) &&
                new FileInfo(path).Length > 0;
        } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            return false;
        }
    }

    private static bool IsReparsePoint(string path) {
        try {
            return File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);
        } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            return false;
        }
    }

    private static SourceSnapshot SnapshotSource(string path) {
        var info = new FileInfo(path);
        return new SourceSnapshot(info.Length, info.LastWriteTimeUtc.Ticks);
    }

    private static string HashToken(string sourceKey) =>
        HexToken(SHA256.HashData(Encoding.UTF8.GetBytes(sourceKey)));

    private static string HexToken(ReadOnlySpan<byte> hash) =>
        Convert.ToHexString(hash[..16]).ToLowerInvariant();

    private static void TryDeleteDirectory(string path) {
        try {
            if (Directory.Exists(path)) {
                Directory.Delete(path, recursive: true);
            }
        } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            // Best effort: staging is never referenced by persistence or served as a stable asset.
        }
    }

    private static void TryDeleteFile(string path) {
        try {
            if (File.Exists(path) || IsReparsePoint(path)) {
                File.Delete(path);
            }
        } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            // Best effort rollback for a generation that persistence never observed.
        }
    }

    private readonly record struct SourceSnapshot(long Length, long ModifiedTicks);
}

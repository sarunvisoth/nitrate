using Prismedia.Application.Files;
using Prismedia.Contracts.Media;
using Prismedia.Domain.Entities;

namespace Prismedia.Infrastructure.Media.Processing;

/// <summary>Shared safety and completeness checks for persisted subtitle cache assets.</summary>
public static class SubtitleAssetAvailability {
    /// <summary>Whether a managed track has every asset needed to serve and render it.</summary>
    public static bool IsManagedTrackAvailable(
        AssetPathService? paths,
        Guid entityId,
        EntitySubtitleSource source,
        string storagePath,
        string sourceFormat,
        string? sourcePath) {
        if (source == EntitySubtitleSource.Sidecar) {
            if (paths is null || !IsOwnedOrdinaryFile(paths, entityId, storagePath)) {
                return false;
            }

            return !SubtitleFormats.IsStyled(sourceFormat) ||
                IsStyledSidecarPairAvailable(
                    paths, entityId, storagePath, sourcePath, sourceFormat);
        }

        return IsOrdinaryNonEmptyFile(storagePath);
    }

    /// <summary>Whether a path is an ordinary, non-empty file directly in the entity subtitle cache.</summary>
    public static bool IsOwnedOrdinaryFile(
        AssetPathService paths,
        Guid entityId,
        string? path) =>
        path is not null &&
        paths.IsSubtitleAssetPath(entityId, path) &&
        IsOrdinaryNonEmptyFile(path);

    /// <summary>Whether a styled sidecar's normalized and raw cache files form the expected pair.</summary>
    public static bool IsStyledSidecarPairAvailable(
        AssetPathService paths,
        Guid entityId,
        string storagePath,
        string? sourcePath,
        string sourceFormat) {
        if (!SubtitleFormats.IsStyled(sourceFormat) ||
            !IsOwnedOrdinaryFile(paths, entityId, storagePath) ||
            !IsOwnedOrdinaryFile(paths, entityId, sourcePath)) {
            return false;
        }

        try {
            var expectedSourcePath = Path.ChangeExtension(
                Path.GetFullPath(storagePath),
                SubtitleFileExtensions.ForFormat(sourceFormat));
            return FileSystemPathComparison.Equals(
                expectedSourcePath,
                Path.GetFullPath(sourcePath!));
        } catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException) {
            return false;
        }
    }

    /// <summary>Whether a path resolves to an ordinary, non-link, non-empty file.</summary>
    public static bool IsOrdinaryNonEmptyFile(string? path) {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)) {
            return false;
        }

        try {
            var attributes = File.GetAttributes(path);
            return !attributes.HasFlag(FileAttributes.Directory) &&
                !attributes.HasFlag(FileAttributes.ReparsePoint) &&
                new FileInfo(path).Length > 0;
        } catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) {
            return false;
        }
    }
}

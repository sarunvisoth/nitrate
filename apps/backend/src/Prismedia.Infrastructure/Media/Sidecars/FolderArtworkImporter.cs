using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prismedia.Application.Files;
using Prismedia.Application.Jobs.Ports;
using Prismedia.Contracts.Media;
using Prismedia.Domain.Entities;
using Prismedia.Infrastructure.Entities;
using Prismedia.Infrastructure.Media.Processing;
using Prismedia.Infrastructure.Persistence;
using Prismedia.Infrastructure.Persistence.Entities;

namespace Prismedia.Infrastructure.Media.Sidecars;

/// <summary>
/// Copies folder artwork (<c>cover.jpg</c>, <c>folder.jpg</c>, <c>artist.jpg</c>, …) into the asset cache
/// and records it as the entity's scan-sourced <see cref="EntityFileRole.Cover"/>. The cached file name
/// carries a fingerprint of the source file, so a replaced image is picked up on the next scan while an
/// unchanged one is skipped. Rows whose source is not <see cref="FileSourceKind.Scan"/> win outright.
/// </summary>
public sealed class FolderArtworkImporter(
    PrismediaDbContext db,
    EntityImageAssetStorageOptions options,
    IGridThumbnailService gridThumbnails,
    ILogger<FolderArtworkImporter> logger) : IFolderArtworkImporter {
    /// <summary>Cache subdirectory holding imported folder artwork, served under <c>/assets</c>.</summary>
    internal const string AssetDirectory = "folder-artwork";

    // Checked in order; the first existing file wins.
    private static readonly string[] AlbumImageNames = ["cover", "folder", "front", "album"];
    private static readonly string[] ArtistImageNames = ["artist", "folder", "poster"];
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    /// <inheritdoc />
    public async Task<int> ImportMusicFolderArtworkAsync(Guid rootId, CancellationToken cancellationToken) {
        var albumCode = EntityKind.AudioLibrary.ToCode();
        var artistCode = EntityKind.MusicArtist.ToCode();
        var folderCode = EntitySourceCode.Folder.ToCode();
        var containers = await (
                from link in db.EntityLibraryRoots.AsNoTracking()
                where link.LibraryRootId == rootId
                join entity in db.Entities.AsNoTracking() on link.EntityId equals entity.Id
                where entity.KindCode == albumCode || entity.KindCode == artistCode
                join source in db.EntitySources.AsNoTracking() on entity.Id equals source.EntityId
                where source.Code == folderCode
                select new { entity.Id, entity.KindCode, Folder = source.Value })
            .ToListAsync(cancellationToken);

        var imported = 0;
        foreach (var container in containers) {
            cancellationToken.ThrowIfCancellationRequested();
            var names = container.KindCode == artistCode ? ArtistImageNames : AlbumImageNames;
            try {
                if (await ImportAsync(container.Id, container.Folder, names, cancellationToken)) {
                    imported++;
                }
            } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                logger.LogWarning(ex, "Folder artwork: could not import an image from {Folder}", container.Folder);
            }
        }

        if (imported > 0) {
            logger.LogInformation("Folder artwork: imported {Count} artist/album covers", imported);
        }

        return imported;
    }

    private async Task<bool> ImportAsync(Guid entityId, string folder, string[] names, CancellationToken cancellationToken) {
        var image = FindImage(folder, names);
        if (image is null) {
            return false;
        }

        var existing = await db.EntityFiles.FirstOrDefaultAsync(
            row => row.EntityId == entityId && row.Role == EntityFileRole.Cover, cancellationToken);
        if (existing is not null && existing.Source != FileSourceKind.Scan.ToCode()) {
            return false;
        }

        var extension = image.Extension.ToLowerInvariant() is ".jpeg" ? ".jpg" : image.Extension.ToLowerInvariant();
        var relativePath = Path.Combine(AssetDirectory, entityId.ToString(), $"cover-{Fingerprint(image)}{extension}");
        var publicPath = $"{AssetPaths.AssetsUrlPrefix}{relativePath.Replace(Path.DirectorySeparatorChar, '/')}";
        var physicalPath = Path.Combine(options.CacheRoot, relativePath);
        if (existing?.Path == publicPath && File.Exists(physicalPath)) {
            return false;
        }

        var entityDirectory = Path.GetDirectoryName(physicalPath)!;
        Directory.CreateDirectory(entityDirectory);
        File.Copy(image.FullName, physicalPath, overwrite: true);
        foreach (var previous in Directory.EnumerateFiles(entityDirectory)) {
            if (!FileSystemPathComparison.Equals(previous, physicalPath)) {
                TryDelete(previous);
            }
        }

        var now = DateTimeOffset.UtcNow;
        var mimeType = MimeTypeFor(extension);
        if (existing is null) {
            db.EntityFiles.Add(new EntityFileRow {
                Id = Guid.NewGuid(),
                EntityId = entityId,
                Role = EntityFileRole.Cover,
                Path = publicPath,
                MimeType = mimeType,
                SizeBytes = image.Length,
                Source = FileSourceKind.Scan.ToCode(),
                CreatedAt = now,
                UpdatedAt = now
            });
        } else {
            existing.Path = publicPath;
            existing.MimeType = mimeType;
            existing.SizeBytes = image.Length;
            existing.UpdatedAt = now;
        }

        var entity = await db.Entities.FirstOrDefaultAsync(row => row.Id == entityId, cancellationToken);
        if (entity is not null) {
            entity.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        await gridThumbnails.EnsureAsync(entityId, cancellationToken);
        return true;
    }

    private static FileInfo? FindImage(string folder, string[] names) {
        if (!Directory.Exists(folder)) {
            return null;
        }

        // Match case-insensitively on every platform ("Cover.JPG" is common).
        var files = new DirectoryInfo(folder).EnumerateFiles()
            .Where(file => !file.Attributes.HasFlag(FileAttributes.Hidden))
            .ToArray();
        foreach (var name in names) {
            foreach (var extension in ImageExtensions) {
                var match = files.FirstOrDefault(file =>
                    file.Name.Equals(name + extension, StringComparison.OrdinalIgnoreCase));
                if (match is not null) {
                    return match;
                }
            }
        }

        return null;
    }

    private static string Fingerprint(FileInfo image) {
        var identity = $"{image.FullName}|{image.Length}|{image.LastWriteTimeUtc.Ticks}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..12].ToLowerInvariant();
    }

    private static string MimeTypeFor(string extension) =>
        extension switch {
            ".png" => MediaContentTypes.ImagePng,
            ".webp" => MediaContentTypes.ImageWebp,
            _ => MediaContentTypes.ImageJpeg
        };

    private void TryDelete(string path) {
        try {
            File.Delete(path);
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            logger.LogDebug(ex, "Folder artwork: could not remove superseded {Path}", path);
        }
    }
}

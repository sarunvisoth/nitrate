namespace Prismedia.Application.Jobs.Ports;

/// <summary>
/// Adopts artwork images that sit next to media on disk — <c>cover.jpg</c> in an album folder,
/// <c>artist.jpg</c> in an artist folder — as the owning entity's cover.
/// </summary>
public interface IFolderArtworkImporter {
    /// <summary>
    /// Imports folder artwork for every music artist and album in a library root. Custom artwork
    /// (manual uploads and plugin downloads) is never replaced, and an unchanged folder image is
    /// not copied again, so calling this on every scan is cheap.
    /// </summary>
    /// <param name="rootId">Library root whose artists and albums should be checked.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of entities whose cover was written or replaced.</returns>
    Task<int> ImportMusicFolderArtworkAsync(Guid rootId, CancellationToken cancellationToken);
}

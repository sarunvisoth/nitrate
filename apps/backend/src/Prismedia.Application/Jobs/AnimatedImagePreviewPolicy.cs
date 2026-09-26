namespace Prismedia.Application.Jobs;

/// <summary>
/// Identifies gallery image files that are actually video-backed animated media and therefore need
/// a generated MP4 preview clip before the frontend should autoplay them in feeds or lightboxes.
/// </summary>
public static class AnimatedImagePreviewPolicy {
    private static readonly HashSet<string> VideoLikeImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".webm",
        ".mp4",
        ".m4v",
        ".mkv",
        ".mov",
        ".avi",
        ".wmv",
        ".flv"
    };

    /// <summary>
    /// Returns true when <paramref name="filePath"/> is a gallery item whose original source should
    /// be represented by a lightweight generated MP4 preview for browsing playback.
    /// </summary>
    public static bool RequiresPreviewClip(string filePath) =>
        VideoLikeImageExtensions.Contains(Path.GetExtension(filePath));
}

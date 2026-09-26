namespace Prismedia.Infrastructure.Media.Persistence;

internal static class LibraryScanFileSystem {
    public static long? TryGetFileSize(string path) {
        try { return new FileInfo(path).Length; } catch { return null; }
    }
}

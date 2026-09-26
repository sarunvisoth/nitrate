using System.Security;
using System.Security.Cryptography;
using System.Text;
using Prismedia.Application.Files;
using Prismedia.Application.Jobs.Ports;
using Prismedia.Application.Jobs.Scanning;
using Prismedia.Contracts.Media;
using Prismedia.Infrastructure.Media.Processing;

namespace Prismedia.Infrastructure.Media.Sidecars;

/// <summary>
/// Enumerates adjacent subtitle files once per video directory and assigns each candidate to the
/// longest matching sibling-video stem.
/// </summary>
public sealed class SubtitleSidecarDiscovery : ISubtitleSidecarDiscovery {
    /// <inheritdoc />
    public Task<IReadOnlyList<VideoSubtitleSidecarDiscovery>> DiscoverAsync(
        IReadOnlyCollection<string> videoPaths,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(videoPaths);
        cancellationToken.ThrowIfCancellationRequested();

        var requests = videoPaths.Select(CreateRequest).ToArray();
        var candidatesByVideo = requests
            .Select(request => request.VideoPath)
            .Distinct(FileSystemPathComparison.Comparer)
            .ToDictionary(
                path => path,
                _ => new List<SubtitleSidecarCandidate>(),
                FileSystemPathComparison.Comparer);
        var completenessByVideo = candidatesByVideo.Keys.ToDictionary(
            path => path,
            _ => true,
            FileSystemPathComparison.Comparer);

        foreach (var request in requests.Where(request => request.Directory is null)) {
            completenessByVideo[request.VideoPath] = false;
        }

        foreach (var group in requests
                     .Where(request => request.Directory is not null)
                     .GroupBy(
                         request => request.Directory!,
                         FileSystemPathComparison.Comparer)) {
            DiscoverDirectory(
                group.Key,
                group.ToArray(),
                candidatesByVideo,
                completenessByVideo,
                cancellationToken);
        }

        var discoveriesByVideo = candidatesByVideo.ToDictionary(
            pair => pair.Key,
            pair => CreateDiscovery(pair.Key, pair.Value, completenessByVideo[pair.Key]),
            FileSystemPathComparison.Comparer);
        IReadOnlyList<VideoSubtitleSidecarDiscovery> results = requests
            .Select(request => discoveriesByVideo[request.VideoPath])
            .ToArray();
        return Task.FromResult(results);
    }

    private static VideoRequest CreateRequest(string path) {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var videoPath = Path.GetFullPath(path);
        return new VideoRequest(
            videoPath,
            Path.GetDirectoryName(videoPath),
            Path.GetFileNameWithoutExtension(videoPath));
    }

    private static void DiscoverDirectory(
        string directory,
        IReadOnlyList<VideoRequest> requests,
        IReadOnlyDictionary<string, List<SubtitleSidecarCandidate>> candidatesByVideo,
        IDictionary<string, bool> completenessByVideo,
        CancellationToken cancellationToken) {
        if (!TryEnumerateFiles(directory, cancellationToken, out var entries)) {
            MarkIncomplete(requests.Select(request => request.VideoPath), completenessByVideo);
            return;
        }

        var requestedPathsByStem = requests
            .GroupBy(request => request.Stem, FileSystemPathComparison.Comparer)
            .ToDictionary(
                group => group.Key,
                group => group.Select(request => request.VideoPath).ToArray(),
                FileSystemPathComparison.Comparer);
        var siblingStems = entries
            .Where(path => SupportedExtensions.Video.Contains(Path.GetExtension(path)))
            .Select(Path.GetFileNameWithoutExtension)
            .Concat(requests.Select(request => request.Stem))
            .OfType<string>()
            .Where(stem => stem.Length > 0)
            .Distinct(FileSystemPathComparison.Comparer)
            .OrderByDescending(stem => stem.Length)
            .ThenBy(stem => stem, FileSystemPathComparison.Comparer)
            .ToArray();

        foreach (var path in entries) {
            cancellationToken.ThrowIfCancellationRequested();
            var format = FormatForExtension(Path.GetExtension(path));
            if (format is null) {
                continue;
            }

            var sidecarStem = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrEmpty(sidecarStem)) {
                continue;
            }

            var ownerStem = siblingStems.FirstOrDefault(stem =>
                SubtitleSidecarNameParser.Parse(stem, sidecarStem) is not null);
            if (ownerStem is null || !requestedPathsByStem.TryGetValue(ownerStem, out var videoOwners)) {
                continue;
            }

            var parsed = SubtitleSidecarNameParser.Parse(ownerStem, sidecarStem)!;
            var readStatus = TryReadCandidate(path, out var fileState);
            if (readStatus == CandidateReadStatus.Failed) {
                MarkIncomplete(videoOwners, completenessByVideo);
                continue;
            }

            if (readStatus == CandidateReadStatus.Unsafe) {
                continue;
            }

            var candidate = new SubtitleSidecarCandidate(
                path,
                SourceKeyFor(path),
                format,
                parsed.Language,
                parsed.Label,
                fileState.SizeBytes,
                fileState.ModifiedTicks);
            foreach (var videoPath in videoOwners) {
                candidatesByVideo[videoPath].Add(candidate);
            }
        }
    }

    private static bool TryEnumerateFiles(
        string directory,
        CancellationToken cancellationToken,
        out IReadOnlyList<string> entries) {
        try {
            var discovered = new List<string>();
            foreach (var path in Directory.EnumerateFiles(directory)) {
                cancellationToken.ThrowIfCancellationRequested();
                discovered.Add(Path.GetFullPath(path));
            }

            entries = discovered;
            return true;
        } catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or SecurityException) {
            entries = [];
            return false;
        }
    }

    private static CandidateReadStatus TryReadCandidate(string path, out CandidateFileState state) {
        try {
            var info = new FileInfo(path);
            info.Refresh();
            var attributes = info.Attributes;
            if (attributes.HasFlag(FileAttributes.ReparsePoint) ||
                attributes.HasFlag(FileAttributes.Directory)) {
                state = default;
                return CandidateReadStatus.Unsafe;
            }

            if (!info.Exists) {
                state = default;
                return CandidateReadStatus.Failed;
            }

            state = new CandidateFileState(info.Length, info.LastWriteTimeUtc.Ticks);
            return CandidateReadStatus.Success;
        } catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or SecurityException) {
            state = default;
            return CandidateReadStatus.Failed;
        }
    }

    private static string? FormatForExtension(string extension) {
        if (!SubtitleFileExtensions.Supported.Contains(extension)) {
            return null;
        }

        if (string.Equals(extension, SubtitleFileExtensions.Srt, StringComparison.OrdinalIgnoreCase)) {
            return SubtitleFormats.Srt;
        }

        if (string.Equals(extension, SubtitleFileExtensions.Vtt, StringComparison.OrdinalIgnoreCase)) {
            return SubtitleFormats.Vtt;
        }

        if (string.Equals(extension, SubtitleFileExtensions.Ass, StringComparison.OrdinalIgnoreCase)) {
            return SubtitleFormats.Ass;
        }

        return SubtitleFormats.Ssa;
    }

    private static string SourceKeyFor(string path) {
        var fileName = Path.GetFileName(path);
        var platformIdentity = OperatingSystem.IsWindows() ? fileName.ToUpperInvariant() : fileName;
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(platformIdentity)));
    }

    private static VideoSubtitleSidecarDiscovery CreateDiscovery(
        string videoPath,
        IReadOnlyList<SubtitleSidecarCandidate> candidates,
        bool isComplete) {
        var sorted = candidates
            .OrderBy(candidate => candidate.SourceKey, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Path, StringComparer.Ordinal)
            .ToArray();
        return new VideoSubtitleSidecarDiscovery(videoPath, sorted, ComputeSignature(sorted), isComplete);
    }

    private static string ComputeSignature(IReadOnlyList<SubtitleSidecarCandidate> candidates) {
        using var descriptor = new MemoryStream();
        using (var writer = new BinaryWriter(descriptor, new UTF8Encoding(false), leaveOpen: true)) {
            foreach (var candidate in candidates.OrderBy(candidate => candidate.SourceKey, StringComparer.Ordinal)) {
                writer.Write(candidate.SourceKey);
                writer.Write(candidate.SizeBytes);
                writer.Write(candidate.ModifiedTicks);
            }
        }

        return Convert.ToHexStringLower(SHA256.HashData(descriptor.ToArray()));
    }

    private static void MarkIncomplete(
        IEnumerable<string> videoPaths,
        IDictionary<string, bool> completenessByVideo) {
        foreach (var videoPath in videoPaths) {
            completenessByVideo[videoPath] = false;
        }
    }

    private sealed record VideoRequest(string VideoPath, string? Directory, string Stem);

    private readonly record struct CandidateFileState(long SizeBytes, long ModifiedTicks);

    private enum CandidateReadStatus {
        Success,
        Unsafe,
        Failed,
    }
}

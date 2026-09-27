using Prismedia.Application.Jobs;
using Microsoft.Extensions.Logging;
using Prismedia.Contracts.Settings;

namespace Prismedia.Application.Settings;

/// <summary>
/// Watched library-root use cases for <see cref="SettingsService"/>: listing, folder
/// browsing, and create/update/delete of roots (including kicking off the initial scan).
/// </summary>
public sealed partial class SettingsService {
    /// <summary>
    /// Returns the registry catalog plus watched roots for the settings page.
    /// </summary>
    public async Task<LibraryConfigResponse> GetLibraryConfigAsync(CancellationToken cancellationToken) {
        var catalog = await GetCatalogAsync(cancellationToken);
        var roots = await _persistence.ListLibraryRootsAsync(cancellationToken);
        return new LibraryConfigResponse(catalog, roots);
    }

    /// <summary>
    /// Lists every watched library root in stable display order.
    /// </summary>
    public Task<IReadOnlyList<LibraryRoot>> ListLibraryRootsAsync(CancellationToken cancellationToken) =>
        _persistence.ListLibraryRootsAsync(cancellationToken);

    /// <summary>
    /// Finds one watched library root, or null when it does not exist.
    /// </summary>
    public Task<LibraryRoot?> GetLibraryRootAsync(Guid id, CancellationToken cancellationToken) =>
        _persistence.GetLibraryRootAsync(id, cancellationToken);

    /// <summary>
    /// Pseudo-path for the Windows drive list ("This PC"). Browsing it returns every ready drive as
    /// a child directory with an empty <see cref="LibraryBrowseResponse.Path"/>, and it is the
    /// parent of each drive root, so the picker can move between drives.
    /// </summary>
    public const string DriveListBrowsePath = "::drives";

    /// <summary>
    /// Lists subdirectories under <paramref name="path"/> for the watched-root folder picker.
    /// Falls back to the user profile directory or the filesystem root when no readable path is
    /// supplied. On Windows, a missing path opens the drive list, and a drive root's parent is
    /// <see cref="DriveListBrowsePath"/>.
    /// </summary>
    public Task<LibraryBrowseResponse> BrowseLibraryPathAsync(string? path, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        // On Windows the server usually runs as SYSTEM, whose profile directory
        // (C:\Windows\system32\config\systemprofile) is a useless starting point, so start at the drives.
        if (OperatingSystem.IsWindows() && (path == DriveListBrowsePath || string.IsNullOrWhiteSpace(path))) {
            return Task.FromResult(BrowseDriveList());
        }

        var requestedPath = string.IsNullOrWhiteSpace(path)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : path.Trim();
        var directory = ResolveBrowseDirectory(requestedPath);
        if (directory is null) {
            return Task.FromResult(OperatingSystem.IsWindows()
                ? BrowseDriveList()
                : BrowseDirectory(new DirectoryInfo("/")));
        }

        return Task.FromResult(BrowseDirectory(directory));
    }

    private static DirectoryInfo? ResolveBrowseDirectory(string requestedPath) {
        try {
            var directory = new DirectoryInfo(requestedPath);
            if (directory.Exists) {
                return directory;
            }

            var root = Path.GetPathRoot(directory.FullName);
            return string.IsNullOrEmpty(root) || !Directory.Exists(root) ? null : new DirectoryInfo(root);
        } catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException) {
            return null;
        }
    }

    private static LibraryBrowseResponse BrowseDirectory(DirectoryInfo directory) {
        LibraryBrowseEntry[] directories;
        try {
            directories = directory.EnumerateDirectories()
                .Where(child => !child.Attributes.HasFlag(FileAttributes.Hidden))
                .OrderBy(child => child.Name)
                .Select(child => new LibraryBrowseEntry(child.Name, child.FullName))
                .ToArray();
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            directories = [];
        }

        var parentPath = directory.Parent?.FullName
            ?? (OperatingSystem.IsWindows() ? DriveListBrowsePath : null);
        return new LibraryBrowseResponse(directory.FullName, parentPath, directories);
    }

    private static LibraryBrowseResponse BrowseDriveList() {
        var drives = DriveInfo.GetDrives()
            .Where(drive => drive.IsReady)
            .Select(drive => new LibraryBrowseEntry(DescribeDrive(drive), drive.RootDirectory.FullName))
            .ToArray();
        return new LibraryBrowseResponse(string.Empty, null, drives);
    }

    private static string DescribeDrive(DriveInfo drive) {
        string label;
        try {
            label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? drive.DriveType.ToString() : drive.VolumeLabel;
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            label = drive.DriveType.ToString();
        }

        return $"{drive.Name.TrimEnd('\\')} ({label})";
    }

    /// <summary>
    /// Adds a new watched media root. The label defaults to the trailing directory name when
    /// omitted by the caller, and falls back to the raw path when the directory name is empty.
    /// When the root is enabled, a scan job is queued immediately for each enabled media kind
    /// so newly added libraries begin scanning right away rather than waiting for the optional
    /// recurring auto-scan (which is off by default).
    /// </summary>
    public async Task<LibraryRoot> CreateLibraryRootAsync(
        LibraryRootCreateRequest request,
        CancellationToken cancellationToken,
        Guid? createdByUserId = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Path);

        var now = DateTimeOffset.UtcNow;
        var label = string.IsNullOrWhiteSpace(request.Label)
            ? new DirectoryInfo(request.Path).Name
            : request.Label.Trim();
        if (string.IsNullOrWhiteSpace(label)) {
            label = request.Path;
        }

        var state = new LibraryRoot(
            Id: Guid.NewGuid(),
            Path: request.Path,
            Label: label,
            Enabled: request.Enabled ?? true,
            Recursive: request.Recursive ?? true,
            ScanVideos: request.ScanVideos ?? true,
            ScanImages: request.ScanImages ?? true,
            ScanAudio: request.ScanAudio ?? true,
            ScanBooks: request.ScanBooks ?? false,
            IsNsfw: request.IsNsfw ?? false,
            LastScannedAt: null,
            CreatedAt: now,
            UpdatedAt: now,
            AutoIdentify: request.AutoIdentify ?? true,
            CreatedByUserId: createdByUserId);

        var created = await _persistence.AddLibraryRootAsync(state, cancellationToken);

        if (created.Enabled && _jobs is not null) {
            var queued = await LibraryScanJobs.QueueScansForRootAsync(
                _jobs,
                created.Id,
                created.Label,
                new LibraryScanSelection(
                    Videos: created.ScanVideos,
                    Images: created.ScanImages,
                    Audio: created.ScanAudio,
                    Books: created.ScanBooks,
                    Comics: created.ScanBooks),
                cancellationToken);
            _logger?.LogInformation(
                "Queued {Count} scan job(s) after adding library root '{Label}'.",
                queued, created.Label);
        }

        return created;
    }

    /// <summary>
    /// Partially updates one watched media root. Returns null when no root with the supplied id exists.
    /// </summary>
    public async Task<LibraryRoot?> UpdateLibraryRootAsync(
        Guid id,
        LibraryRootUpdateRequest request,
        CancellationToken cancellationToken) {
        var current = await _persistence.GetLibraryRootAsync(id, cancellationToken);
        if (current is null) {
            return null;
        }

        var next = current with {
            Path = !string.IsNullOrWhiteSpace(request.Path) ? request.Path : current.Path,
            Label = request.Label ?? current.Label,
            Enabled = request.Enabled ?? current.Enabled,
            Recursive = request.Recursive ?? current.Recursive,
            ScanVideos = request.ScanVideos ?? current.ScanVideos,
            ScanImages = request.ScanImages ?? current.ScanImages,
            ScanAudio = request.ScanAudio ?? current.ScanAudio,
            ScanBooks = request.ScanBooks ?? current.ScanBooks,
            IsNsfw = request.IsNsfw ?? current.IsNsfw,
            AutoIdentify = request.AutoIdentify ?? current.AutoIdentify,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        return await _persistence.SaveLibraryRootAsync(next, cancellationToken);
    }

    /// <summary>
    /// Records that a recurring scan was triggered for one watched media root.
    /// The timestamp marks scheduler intent rather than scan completion.
    /// </summary>
    /// <param name="id">Watched root identifier.</param>
    /// <param name="triggeredAt">UTC time when the scheduler triggered the scan.</param>
    /// <param name="cancellationToken">Token to cancel the persistence operation.</param>
    public async Task<LibraryRoot?> MarkLibraryRootScanTriggeredAsync(
        Guid id,
        DateTimeOffset triggeredAt,
        CancellationToken cancellationToken) {
        var current = await _persistence.GetLibraryRootAsync(id, cancellationToken);
        if (current is null) {
            return null;
        }

        return await _persistence.SaveLibraryRootAsync(current with {
            LastScannedAt = triggeredAt,
            UpdatedAt = DateTimeOffset.UtcNow,
        }, cancellationToken);
    }

    /// <summary>
    /// Removes one watched media root.
    /// </summary>
    public Task<bool> DeleteLibraryRootAsync(Guid id, CancellationToken cancellationToken) =>
        _persistence.DeleteLibraryRootAsync(id, cancellationToken);
}

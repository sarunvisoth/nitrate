namespace Prismedia.Application.Settings;

/// <summary>
/// Raised when a request references an unknown app-global setting key.
/// </summary>
public sealed class SettingNotFoundException : KeyNotFoundException {
    public SettingNotFoundException(string key)
        : base($"Setting '{key}' was not found.") {
        Key = key;
    }

    /// <summary>Unknown setting key.</summary>
    public string Key { get; }
}

/// <summary>
/// Raised when a setting value fails registry validation.
/// </summary>
public sealed class SettingValidationException : ArgumentException {
    public SettingValidationException(string key, string message)
        : base(message) {
        Key = key;
    }

    /// <summary>Setting key whose value failed validation.</summary>
    public string Key { get; }
}

/// <summary>
/// Raised when a watched library root uses a path that already belongs to another root.
/// </summary>
public sealed class LibraryRootPathConflictException : InvalidOperationException {
    /// <summary>
    /// Creates a conflict for an already-watched path while preserving the persistence failure.
    /// </summary>
    /// <param name="path">Path that already belongs to a watched library root.</param>
    /// <param name="innerException">Underlying persistence exception, when available.</param>
    public LibraryRootPathConflictException(string path, Exception? innerException = null)
        : base($"A library root already exists at '{path}'.", innerException) {
        Path = path;
    }

    /// <summary>Path that already belongs to a watched library root.</summary>
    public string Path { get; }
}

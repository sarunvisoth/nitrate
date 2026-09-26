namespace Prismedia.Infrastructure.Plugins;

/// <summary>
/// Resolves plugin credentials from stored provider credentials and canonical environment variables.
/// </summary>
public static class PluginCredentialResolver {
    /// <summary>
    /// Returns true when the stored credential key exactly matches the manifest field.
    /// </summary>
    public static bool HasCredentialForField(ISet<string> keys, string key) =>
        keys.Contains(key);

    /// <summary>
    /// Returns true when a canonical environment variable can satisfy the manifest field.
    /// </summary>
    public static bool HasEnvironmentCredential(string providerId, string key) =>
        !string.IsNullOrWhiteSpace(ResolveEnvironmentCredential(providerId, key));

    /// <summary>
    /// Reads the canonical environment variable for a plugin credential field.
    /// </summary>
    public static string? ResolveEnvironmentCredential(string providerId, string key) {
        var value = Environment.GetEnvironmentVariable(EnvironmentCredentialKey(providerId, key));
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string EnvironmentCredentialKey(string providerId, string key) =>
        $"PRISMEDIA_PLUGIN_{EnvironmentSegment(providerId)}_{EnvironmentSegment(key)}";

    private static string EnvironmentSegment(string value) {
        var chars = new List<char>();
        var previousWasSeparator = true;
        for (var index = 0; index < value.Length; index++) {
            var current = value[index];
            if (!char.IsLetterOrDigit(current)) {
                if (!previousWasSeparator) {
                    chars.Add('_');
                    previousWasSeparator = true;
                }
                continue;
            }

            if (char.IsUpper(current) && !previousWasSeparator &&
                index > 0 && char.IsLower(value[index - 1])) {
                chars.Add('_');
            }

            chars.Add(char.ToUpperInvariant(current));
            previousWasSeparator = false;
        }

        return new string(chars.ToArray()).Trim('_');
    }
}

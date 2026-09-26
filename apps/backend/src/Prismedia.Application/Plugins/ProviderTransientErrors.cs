namespace Prismedia.Application.Plugins;

/// <summary>
/// Classifies provider error text as transient (rate limit, timeout, temporary outage) so identify
/// jobs defer and retry later instead of recording a permanent no-match — and instead of falling
/// through to another provider call that would hammer an already rate-limited upstream.
/// </summary>
public static class ProviderTransientErrors {
    /// <summary>Whether the provider error text describes a temporary condition worth retrying.</summary>
    /// <param name="error">Provider error message, if any.</param>
    public static bool IsRetryable(string? error) {
        if (string.IsNullOrWhiteSpace(error)) {
            return false;
        }

        return error.Contains("429", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("too many requests", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("temporarily", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("503", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("service unavailable", StringComparison.OrdinalIgnoreCase);
    }
}

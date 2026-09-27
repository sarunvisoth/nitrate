using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Prismedia.Api.Security;

/// <summary>
/// Configures <see cref="ForwardedHeadersMiddleware"/> so the API reports the correct
/// scheme/host/client-IP when reached through a reverse proxy that terminates TLS on
/// another hostname (Tailscale Serve, Cloudflare Tunnel, a Caddy/nginx front end). Both
/// Tailscale Serve and cloudflared connect to the API from the same machine
/// (<c>localhost</c>), so the safe default trusts loopback only — the same default the
/// ASP.NET Core middleware ships with. A deployment that fronts Nitrate with a proxy on a
/// different host (e.g. a separate reverse-proxy container) can extend the trusted set
/// with <c>PRISMEDIA_TRUSTED_PROXIES</c>, a comma-separated list of proxy IP addresses.
/// </summary>
internal static class ForwardedHeadersSetup {
    /// <summary>Environment/configuration key for additional trusted proxy IPs.</summary>
    internal const string TrustedProxiesConfigKey = "PRISMEDIA_TRUSTED_PROXIES";

    internal static ForwardedHeadersOptions Build(IConfiguration configuration) {
        var options = new ForwardedHeadersOptions {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                ForwardedHeaders.XForwardedProto |
                ForwardedHeaders.XForwardedHost,
            // Only the innermost hop is trusted: the local reverse proxy. A chain of
            // forwarded values beyond that is not honoured, so a client cannot forge
            // additional hops to smuggle a spoofed origin past the proxy.
            ForwardLimit = 1,
        };

        foreach (var raw in ParseTrustedProxies(configuration[TrustedProxiesConfigKey])) {
            if (IPAddress.TryParse(raw, out var address)) {
                options.KnownProxies.Add(address);
            }
        }

        return options;
    }

    private static IEnumerable<string> ParseTrustedProxies(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}

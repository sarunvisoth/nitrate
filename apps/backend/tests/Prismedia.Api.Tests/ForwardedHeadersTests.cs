using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Prismedia.Contracts.Security;

namespace Prismedia.Api.Tests;

/// <summary>
/// Exercises <c>UseForwardedHeaders</c> (see <c>Prismedia.Api.Security.ForwardedHeadersSetup</c>
/// and <c>Program.cs</c>): behind a trusted loopback proxy (Tailscale Serve, Cloudflare Tunnel)
/// the API must see the proxy's forwarded scheme/client-IP; a direct caller must not be able to
/// forge those same headers.
/// </summary>
public sealed class ForwardedHeadersTests {
    [Fact]
    public async Task SessionCookieIsSecureBehindATrustedHttpsProxy() {
        using var factory = new WebApplicationFactory<Program>().WithTestAuth();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") {
            Content = JsonContent.Create(new LoginRequest(TestAuth.Username, TestAuth.Password))
        };
        // WebApplicationFactory's TestServer reports the request as coming from loopback by
        // default, which is the same trust boundary Tailscale Serve/cloudflared satisfy in
        // production (both connect to the API from the same machine).
        request.Headers.Add("X-Forwarded-Proto", "https");

        using var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), value =>
            value.StartsWith("prismedia-session=", StringComparison.Ordinal));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SessionCookieStaysPlainOnLanHttpWithNoProxy() {
        using var factory = new WebApplicationFactory<Program>().WithTestAuth();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(TestAuth.Username, TestAuth.Password));

        response.EnsureSuccessStatusCode();
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), value =>
            value.StartsWith("prismedia-session=", StringComparison.Ordinal));
        Assert.DoesNotContain("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SessionCookieIgnoresForwardedProtoFromAnUntrustedDirectCaller() {
        using var factory = new WebApplicationFactory<Program>().WithTestAuth().WithSimulatedRemoteIp();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") {
            Content = JsonContent.Create(new LoginRequest(TestAuth.Username, TestAuth.Password))
        };
        request.Headers.Add(RemoteIpTestSupport.RemoteIpHeader, "203.0.113.9");
        request.Headers.Add("X-Forwarded-Proto", "https");

        using var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), value =>
            value.StartsWith("prismedia-session=", StringComparison.Ordinal));
        Assert.DoesNotContain("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoginThrottleBucketsByTheRealForwardedClientIpBehindATrustedProxy() {
        using var factory = new WebApplicationFactory<Program>().WithTestAuth().WithSimulatedRemoteIp();
        // The simulated remote peer stays loopback (the trusted proxy); only the forwarded
        // X-Forwarded-For value differs per "device" behind it.
        using var attackerClient = factory.CreateClient();
        HttpResponseMessage? attackerResponse = null;
        for (var i = 0; i < 9; i++) {
            attackerResponse?.Dispose();
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") {
                Content = JsonContent.Create(new LoginRequest(TestAuth.Username, $"bad-password-{i}"))
            };
            request.Headers.Add("X-Forwarded-For", "203.0.113.11");
            attackerResponse = await attackerClient.SendAsync(request);
        }

        using (attackerResponse) {
            Assert.Equal(HttpStatusCode.TooManyRequests, attackerResponse!.StatusCode);
        }

        // A second device behind the same proxy, with its own forwarded IP, is unaffected —
        // proving the throttle bucket keys off the real client IP, not the proxy's loopback
        // address (which would otherwise collapse every remote device into one shared bucket).
        using var otherDeviceClient = factory.CreateClient();
        using var goodRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") {
            Content = JsonContent.Create(new LoginRequest(TestAuth.Username, TestAuth.Password))
        };
        goodRequest.Headers.Add("X-Forwarded-For", "203.0.113.12");
        using var goodResponse = await otherDeviceClient.SendAsync(goodRequest);
        Assert.Equal(HttpStatusCode.OK, goodResponse.StatusCode);
    }
}

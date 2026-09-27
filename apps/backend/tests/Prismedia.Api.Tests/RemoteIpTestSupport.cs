using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Prismedia.Api.Tests;

/// <summary>
/// Lets a test simulate the TCP peer address the API sees, so forwarded-headers trust
/// decisions (loopback proxy vs. an untrusted direct caller) can be exercised without a real
/// network. An <see cref="Microsoft.AspNetCore.Hosting.IStartupFilter"/> inserts middleware
/// ahead of everything <c>Program.cs</c> configures (including <c>UseForwardedHeaders</c>),
/// so it can override <see cref="Microsoft.AspNetCore.Http.ConnectionInfo.RemoteIpAddress"/>
/// before the forwarded-headers middleware decides whether to trust the request.
/// </summary>
internal static class RemoteIpTestSupport {
    /// <summary>Request header a test sets to choose the simulated remote peer address.</summary>
    internal const string RemoteIpHeader = "X-Test-Remote-Ip";

    internal static WebApplicationFactory<Program> WithSimulatedRemoteIp(
        this WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddTransient<IStartupFilter, RemoteIpStartupFilter>()));

    private sealed class RemoteIpStartupFilter : IStartupFilter {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app => {
                app.Use(async (context, nextMiddleware) => {
                    if (context.Request.Headers.TryGetValue(RemoteIpHeader, out var value) &&
                        IPAddress.TryParse(value.ToString(), out var address)) {
                        context.Connection.RemoteIpAddress = address;
                    }

                    await nextMiddleware();
                });
                next(app);
            };
    }
}

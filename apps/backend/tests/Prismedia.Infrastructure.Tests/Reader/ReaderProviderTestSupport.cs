using Microsoft.Extensions.Caching.Memory;

namespace Prismedia.Infrastructure.Tests.Reader;

/// <summary>Minimal <see cref="IHttpClientFactory"/> that always returns one client over the fake handler.</summary>
internal sealed class SingleClientHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory {
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}

/// <summary>Records the last request and returns a canned response.</summary>
internal sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler {
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) {
        LastRequest = request;
        return Task.FromResult(responder(request));
    }
}

internal static class ReaderTestFixtures {
    public static IMemoryCache NewCache() => new MemoryCache(new MemoryCacheOptions());
}

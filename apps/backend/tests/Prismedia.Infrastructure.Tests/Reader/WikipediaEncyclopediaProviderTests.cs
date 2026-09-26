using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Prismedia.Infrastructure.Reader;

namespace Prismedia.Infrastructure.Tests.Reader;

public sealed class WikipediaEncyclopediaProviderTests {
    [Fact]
    public async Task ReturnsAPlainTextSummary() {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("""
                {
                  "title": "Ubiquitous computing",
                  "extract": "Ubiquitous computing is a concept in <b>software engineering</b>.",
                  "thumbnail": { "source": "https://upload.example/thumb.jpg" },
                  "content_urls": { "desktop": { "page": "https://en.wikipedia.org/wiki/Ubiquitous_computing" } }
                }
                """, Encoding.UTF8, "application/json")
        });
        var provider = new WikipediaEncyclopediaProvider(
            new SingleClientHttpClientFactory(handler), ReaderTestFixtures.NewCache(),
            NullLogger<WikipediaEncyclopediaProvider>.Instance);

        var result = await provider.SummarizeAsync("ubiquitous computing", "en", CancellationToken.None);

        Assert.True(result.Found);
        Assert.Equal("Ubiquitous computing is a concept in software engineering.", result.Value!.Extract);
        Assert.Equal("https://en.wikipedia.org/wiki/Ubiquitous_computing", result.Value.Url);
        Assert.Equal("https://upload.example/thumb.jpg", result.Value.ThumbnailUrl);
    }

    [Fact]
    public async Task A404ResponseIsACleanNotFound() {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var provider = new WikipediaEncyclopediaProvider(
            new SingleClientHttpClientFactory(handler), ReaderTestFixtures.NewCache(),
            NullLogger<WikipediaEncyclopediaProvider>.Instance);

        var result = await provider.SummarizeAsync("zzzznotanarticle", "en", CancellationToken.None);

        Assert.False(result.Found);
        Assert.False(result.ProviderFailed);
    }

    [Fact]
    public async Task A5xxResponseIsAProviderFailure() {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var provider = new WikipediaEncyclopediaProvider(
            new SingleClientHttpClientFactory(handler), ReaderTestFixtures.NewCache(),
            NullLogger<WikipediaEncyclopediaProvider>.Instance);

        var result = await provider.SummarizeAsync("ubiquitous computing", "en", CancellationToken.None);

        Assert.True(result.ProviderFailed);
    }
}

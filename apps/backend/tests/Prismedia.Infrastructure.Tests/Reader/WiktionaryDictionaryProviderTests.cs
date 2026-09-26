using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Prismedia.Domain.Entities;
using Prismedia.Infrastructure.Reader;

namespace Prismedia.Infrastructure.Tests.Reader;

public sealed class WiktionaryDictionaryProviderTests {
    private const string EnglishPayload = """
        {
          "en": [
            {
              "partOfSpeech": "Adjective",
              "language": "English",
              "definitions": [
                {
                  "definition": "<span>Present, appearing, or found <b>everywhere</b>.</span>",
                  "parsedExamples": [ { "example": "Smartphones are <i>ubiquitous</i> nowadays." } ]
                }
              ]
            }
          ],
          "fr": [
            { "partOfSpeech": "Adjectif", "language": "French", "definitions": [ { "definition": "omniprésent" } ] }
          ]
        }
        """;

    [Fact]
    public async Task StripsHtmlAndFiltersToTheRequestedLanguage() {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(EnglishPayload, Encoding.UTF8, "application/json")
        });
        var provider = new WiktionaryDictionaryProvider(
            new SingleClientHttpClientFactory(handler), ReaderTestFixtures.NewCache(),
            NullLogger<WiktionaryDictionaryProvider>.Instance);

        var result = await provider.DefineAsync("ubiquitous", "en", CancellationToken.None);

        Assert.True(result.Found);
        var response = result.Value!;
        Assert.Equal(ReaderProviderKind.Wiktionary, response.Provider);
        Assert.Equal("Wiktionary, CC BY-SA", response.Attribution);
        var entry = Assert.Single(response.Entries);
        Assert.Equal("Adjective", entry.PartOfSpeech);
        var definition = Assert.Single(entry.Definitions);
        Assert.Equal("Present, appearing, or found everywhere.", definition.Text);
        Assert.Equal("Smartphones are ubiquitous nowadays.", Assert.Single(definition.Examples));
        Assert.Contains("en.wiktionary.org", handler.LastRequest!.RequestUri!.Host);
    }

    [Fact]
    public async Task CachesTheResultForRepeatedLookups() {
        var calls = 0;
        var handler = new RecordingHandler(_ => {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(EnglishPayload, Encoding.UTF8, "application/json")
            };
        });
        var provider = new WiktionaryDictionaryProvider(
            new SingleClientHttpClientFactory(handler), ReaderTestFixtures.NewCache(),
            NullLogger<WiktionaryDictionaryProvider>.Instance);

        await provider.DefineAsync("ubiquitous", "en", CancellationToken.None);
        await provider.DefineAsync("ubiquitous", "en", CancellationToken.None);

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task A404ResponseIsACleanNotFound() {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var provider = new WiktionaryDictionaryProvider(
            new SingleClientHttpClientFactory(handler), ReaderTestFixtures.NewCache(),
            NullLogger<WiktionaryDictionaryProvider>.Instance);

        var result = await provider.DefineAsync("zzzznotaword", "en", CancellationToken.None);

        Assert.False(result.Found);
        Assert.False(result.ProviderFailed);
    }

    [Fact]
    public async Task ALanguageWithNoEntriesIsACleanNotFound() {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(EnglishPayload, Encoding.UTF8, "application/json")
        });
        var provider = new WiktionaryDictionaryProvider(
            new SingleClientHttpClientFactory(handler), ReaderTestFixtures.NewCache(),
            NullLogger<WiktionaryDictionaryProvider>.Instance);

        var result = await provider.DefineAsync("ubiquitous", "de", CancellationToken.None);

        Assert.False(result.Found);
        Assert.False(result.ProviderFailed);
    }

    [Fact]
    public async Task A5xxResponseIsAProviderFailure() {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));
        var provider = new WiktionaryDictionaryProvider(
            new SingleClientHttpClientFactory(handler), ReaderTestFixtures.NewCache(),
            NullLogger<WiktionaryDictionaryProvider>.Instance);

        var result = await provider.DefineAsync("ubiquitous", "en", CancellationToken.None);

        Assert.True(result.ProviderFailed);
        Assert.False(result.Found);
    }

    [Fact]
    public async Task MalformedJsonIsAProviderFailure() {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("{not json", Encoding.UTF8, "application/json")
        });
        var provider = new WiktionaryDictionaryProvider(
            new SingleClientHttpClientFactory(handler), ReaderTestFixtures.NewCache(),
            NullLogger<WiktionaryDictionaryProvider>.Instance);

        var result = await provider.DefineAsync("ubiquitous", "en", CancellationToken.None);

        Assert.True(result.ProviderFailed);
    }
}

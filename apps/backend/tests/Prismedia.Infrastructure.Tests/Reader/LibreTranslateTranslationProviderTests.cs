using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Prismedia.Domain.Entities;
using Prismedia.Infrastructure.Reader;

namespace Prismedia.Infrastructure.Tests.Reader;

public sealed class LibreTranslateTranslationProviderTests {
    [Fact]
    public async Task PostsToTheConfiguredBaseUrlAndReturnsTheTranslation() {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("""{ "translatedText": "Bonjour" }""", Encoding.UTF8, "application/json")
        });
        var provider = new LibreTranslateTranslationProvider(
            new SingleClientHttpClientFactory(handler), ReaderTestFixtures.NewCache(),
            NullLogger<LibreTranslateTranslationProvider>.Instance,
            "http://libretranslate.local:5000/", "secret-key");

        var result = await provider.TranslateAsync("Hello", "en", "fr", CancellationToken.None);

        Assert.True(result.Found);
        Assert.Equal("Bonjour", result.Value!.TranslatedText);
        Assert.Equal(ReaderProviderKind.LibreTranslate, result.Value.Provider);
        var request = handler.LastRequest!;
        Assert.Equal("http://libretranslate.local:5000/translate", request.RequestUri!.ToString());
        var body = await request.Content!.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal("Hello", json.RootElement.GetProperty("q").GetString());
        Assert.Equal("secret-key", json.RootElement.GetProperty("api_key").GetString());
    }

    [Fact]
    public async Task DefaultsSourceToAutoWhenNotProvided() {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(
                """{ "translatedText": "Hallo", "detectedLanguage": { "language": "en" } }""",
                Encoding.UTF8, "application/json")
        });
        var provider = new LibreTranslateTranslationProvider(
            new SingleClientHttpClientFactory(handler), ReaderTestFixtures.NewCache(),
            NullLogger<LibreTranslateTranslationProvider>.Instance,
            "http://libretranslate.local:5000", null);

        var result = await provider.TranslateAsync("Hello", null, "de", CancellationToken.None);

        Assert.Equal("en", result.Value!.SourceLanguage);
    }

    [Fact]
    public async Task A5xxResponseIsAProviderFailure() {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var provider = new LibreTranslateTranslationProvider(
            new SingleClientHttpClientFactory(handler), ReaderTestFixtures.NewCache(),
            NullLogger<LibreTranslateTranslationProvider>.Instance,
            "http://libretranslate.local:5000", null);

        var result = await provider.TranslateAsync("Hello", "en", "fr", CancellationToken.None);

        Assert.True(result.ProviderFailed);
    }
}

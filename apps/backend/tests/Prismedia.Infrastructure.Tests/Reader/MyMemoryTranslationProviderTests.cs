using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Prismedia.Domain.Entities;
using Prismedia.Infrastructure.Reader;

namespace Prismedia.Infrastructure.Tests.Reader;

public sealed class MyMemoryTranslationProviderTests {
    [Fact]
    public async Task TranslatesAndDefaultsTheSourceLanguageToEnglish() {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("""
                { "responseData": { "translatedText": "Bonjour" }, "responseStatus": 200 }
                """, Encoding.UTF8, "application/json")
        });
        var provider = new MyMemoryTranslationProvider(
            new SingleClientHttpClientFactory(handler), ReaderTestFixtures.NewCache(),
            NullLogger<MyMemoryTranslationProvider>.Instance);

        var result = await provider.TranslateAsync("Hello", null, "fr", CancellationToken.None);

        Assert.True(result.Found);
        Assert.Equal("Bonjour", result.Value!.TranslatedText);
        Assert.Equal("en", result.Value.SourceLanguage);
        Assert.Equal(ReaderProviderKind.MyMemory, result.Value.Provider);
        Assert.Contains("langpair=en%7Cfr", handler.LastRequest!.RequestUri!.Query);
    }

    [Fact]
    public async Task UsesTheGivenSourceLanguageWhenProvided() {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("""
                { "responseData": { "translatedText": "Hallo" }, "responseStatus": 200 }
                """, Encoding.UTF8, "application/json")
        });
        var provider = new MyMemoryTranslationProvider(
            new SingleClientHttpClientFactory(handler), ReaderTestFixtures.NewCache(),
            NullLogger<MyMemoryTranslationProvider>.Instance);

        var result = await provider.TranslateAsync("Hello", "en", "de", CancellationToken.None);

        Assert.Equal("Hallo", result.Value!.TranslatedText);
        Assert.Equal("de", result.Value.TargetLanguage);
    }

    [Fact]
    public async Task ANonSuccessResponseStatusIsAProviderFailure() {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("""
                { "responseData": { "translatedText": "" }, "responseStatus": 403 }
                """, Encoding.UTF8, "application/json")
        });
        var provider = new MyMemoryTranslationProvider(
            new SingleClientHttpClientFactory(handler), ReaderTestFixtures.NewCache(),
            NullLogger<MyMemoryTranslationProvider>.Instance);

        var result = await provider.TranslateAsync("Hello", null, "fr", CancellationToken.None);

        Assert.True(result.ProviderFailed);
    }

    [Fact]
    public async Task ATimeoutIsAProviderFailure() {
        var handler = new ThrowingHandler(new TaskCanceledException());
        var provider = new MyMemoryTranslationProvider(
            new SingleClientHttpClientFactory(handler), ReaderTestFixtures.NewCache(),
            NullLogger<MyMemoryTranslationProvider>.Instance);

        var result = await provider.TranslateAsync("Hello", null, "fr", CancellationToken.None);

        Assert.True(result.ProviderFailed);
        Assert.False(result.Found);
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw exception;
    }
}

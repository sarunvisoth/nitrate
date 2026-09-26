using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Prismedia.Application.Reader;
using Prismedia.Contracts.Reader;
using Prismedia.Domain.Entities;

namespace Prismedia.Infrastructure.Reader;

/// <summary>
/// Default <see cref="ITranslationProvider"/>: the free MyMemory translation API. MyMemory's
/// <c>langpair</c> parameter requires an explicit source language (it has no bare-autodetect
/// mode), so a missing source falls back to English.
/// </summary>
public sealed class MyMemoryTranslationProvider : ITranslationProvider {
    /// <summary>Named <see cref="HttpClient"/> registered for MyMemory calls.</summary>
    public const string HttpClientName = "PrismediaReaderMyMemory";

    /// <summary>Source language assumed when the caller does not specify one.</summary>
    public const string FallbackSourceLanguage = "en";

    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<MyMemoryTranslationProvider> _logger;

    public MyMemoryTranslationProvider(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<MyMemoryTranslationProvider> logger) {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ReaderLookupResult<TranslateTextResponse>> TranslateAsync(
        string text, string? sourceLanguage, string targetLanguage, CancellationToken cancellationToken) {
        var source = string.IsNullOrWhiteSpace(sourceLanguage) ? FallbackSourceLanguage : sourceLanguage;
        var cacheKey = $"reader:translate:mymemory:{source}:{targetLanguage}:{text}";
        if (_cache.TryGetValue(cacheKey, out ReaderLookupResult<TranslateTextResponse> cached)) {
            return cached;
        }

        var result = await FetchAsync(text, source, targetLanguage, cancellationToken);
        if (!result.ProviderFailed) {
            _cache.Set(cacheKey, result, CacheDuration);
        }

        return result;
    }

    private async Task<ReaderLookupResult<TranslateTextResponse>> FetchAsync(
        string text, string source, string target, CancellationToken cancellationToken) {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var query = $"q={Uri.EscapeDataString(text)}&langpair={Uri.EscapeDataString(source)}|{Uri.EscapeDataString(target)}";
        var uri = $"https://api.mymemory.translated.net/get?{query}";

        try {
            using var response = await client.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode) {
                return ReaderLookupResult<TranslateTextResponse>.Failed(
                    $"MyMemory returned {(int)response.StatusCode}.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<MyMemoryPayload>(stream, JsonOptions, cancellationToken);
            var translated = payload?.ResponseData?.TranslatedText;
            if (payload is null || string.IsNullOrWhiteSpace(translated) || payload.ResponseStatus is < 200 or >= 300) {
                return ReaderLookupResult<TranslateTextResponse>.Failed("MyMemory returned no translation.");
            }

            return ReaderLookupResult<TranslateTextResponse>.Ok(new TranslateTextResponse(
                text, translated.Trim(), source, target, ReaderProviderKind.MyMemory));
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            return ReaderLookupResult<TranslateTextResponse>.Failed("The MyMemory request timed out.");
        } catch (HttpRequestException ex) {
            _logger.LogDebug(ex, "MyMemory translation request failed.");
            return ReaderLookupResult<TranslateTextResponse>.Failed("MyMemory could not be reached.");
        } catch (JsonException ex) {
            _logger.LogDebug(ex, "MyMemory translation response could not be parsed.");
            return ReaderLookupResult<TranslateTextResponse>.Failed("MyMemory's response could not be parsed.");
        }
    }

    private sealed class MyMemoryPayload {
        [JsonPropertyName("responseData")]
        public MyMemoryResponseData? ResponseData { get; set; }

        [JsonPropertyName("responseStatus")]
        public int ResponseStatus { get; set; }
    }

    private sealed class MyMemoryResponseData {
        [JsonPropertyName("translatedText")]
        public string? TranslatedText { get; set; }
    }
}

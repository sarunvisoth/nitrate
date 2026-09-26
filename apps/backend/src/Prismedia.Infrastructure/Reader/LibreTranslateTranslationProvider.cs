using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Prismedia.Application.Reader;
using Prismedia.Contracts.Reader;
using Prismedia.Domain.Entities;

namespace Prismedia.Infrastructure.Reader;

/// <summary>
/// Self-hosted LibreTranslate <see cref="ITranslationProvider"/>, used instead of
/// <see cref="MyMemoryTranslationProvider"/> when
/// <see cref="Prismedia.Application.Settings.AppSettings.Reader.TranslationLibreUrl"/> is configured.
/// Keeps translated text on the owner's LAN.
/// </summary>
public sealed class LibreTranslateTranslationProvider : ITranslationProvider {
    /// <summary>Named <see cref="HttpClient"/> registered for LibreTranslate calls.</summary>
    public const string HttpClientName = "PrismediaReaderLibreTranslate";

    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<LibreTranslateTranslationProvider> _logger;
    private readonly string _baseUrl;
    private readonly string? _apiKey;

    public LibreTranslateTranslationProvider(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<LibreTranslateTranslationProvider> logger,
        string baseUrl,
        string? apiKey) {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
        _baseUrl = baseUrl.TrimEnd('/');
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey;
    }

    public async Task<ReaderLookupResult<TranslateTextResponse>> TranslateAsync(
        string text, string? sourceLanguage, string targetLanguage, CancellationToken cancellationToken) {
        var source = string.IsNullOrWhiteSpace(sourceLanguage) ? "auto" : sourceLanguage;
        var cacheKey = $"reader:translate:libretranslate:{_baseUrl}:{source}:{targetLanguage}:{text}";
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
        var body = new Dictionary<string, string?> {
            ["q"] = text,
            ["source"] = source,
            ["target"] = target,
            ["format"] = "text",
            ["api_key"] = _apiKey
        };

        try {
            using var response = await client.PostAsJsonAsync($"{_baseUrl}/translate", body, JsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode) {
                return ReaderLookupResult<TranslateTextResponse>.Failed(
                    $"LibreTranslate returned {(int)response.StatusCode}.");
            }

            var payload = await response.Content.ReadFromJsonAsync<LibreTranslatePayload>(JsonOptions, cancellationToken);
            if (payload is null || string.IsNullOrWhiteSpace(payload.TranslatedText)) {
                return ReaderLookupResult<TranslateTextResponse>.Failed("LibreTranslate returned no translation.");
            }

            var resolvedSource = string.IsNullOrWhiteSpace(payload.DetectedLanguage?.Language)
                ? source
                : payload.DetectedLanguage!.Language!;
            return ReaderLookupResult<TranslateTextResponse>.Ok(new TranslateTextResponse(
                text, payload.TranslatedText.Trim(), resolvedSource, target, ReaderProviderKind.LibreTranslate));
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            return ReaderLookupResult<TranslateTextResponse>.Failed("The LibreTranslate request timed out.");
        } catch (HttpRequestException ex) {
            _logger.LogDebug(ex, "LibreTranslate request failed.");
            return ReaderLookupResult<TranslateTextResponse>.Failed("LibreTranslate could not be reached.");
        } catch (JsonException ex) {
            _logger.LogDebug(ex, "LibreTranslate response could not be parsed.");
            return ReaderLookupResult<TranslateTextResponse>.Failed("LibreTranslate's response could not be parsed.");
        }
    }

    private sealed class LibreTranslatePayload {
        [JsonPropertyName("translatedText")]
        public string? TranslatedText { get; set; }

        [JsonPropertyName("detectedLanguage")]
        public LibreTranslateDetectedLanguage? DetectedLanguage { get; set; }
    }

    private sealed class LibreTranslateDetectedLanguage {
        [JsonPropertyName("language")]
        public string? Language { get; set; }
    }
}

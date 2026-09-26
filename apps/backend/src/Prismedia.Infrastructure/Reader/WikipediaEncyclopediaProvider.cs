using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Prismedia.Application.Reader;
using Prismedia.Contracts.Reader;

namespace Prismedia.Infrastructure.Reader;

/// <summary>
/// Default <see cref="IEncyclopediaProvider"/>: Wikipedia's REST page-summary API, cached in
/// memory for an hour.
/// </summary>
public sealed class WikipediaEncyclopediaProvider : IEncyclopediaProvider {
    /// <summary>Named <see cref="HttpClient"/> registered for Wikipedia calls.</summary>
    public const string HttpClientName = "PrismediaReaderWikipedia";

    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<WikipediaEncyclopediaProvider> _logger;

    public WikipediaEncyclopediaProvider(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<WikipediaEncyclopediaProvider> logger) {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ReaderLookupResult<WikipediaSummaryResponse>> SummarizeAsync(
        string term, string language, CancellationToken cancellationToken) {
        var cacheKey = $"reader:wikipedia:{language}:{term.ToLowerInvariant()}";
        if (_cache.TryGetValue(cacheKey, out ReaderLookupResult<WikipediaSummaryResponse> cached)) {
            return cached;
        }

        var result = await FetchAsync(term, language, cancellationToken);
        if (!result.ProviderFailed) {
            _cache.Set(cacheKey, result, CacheDuration);
        }

        return result;
    }

    private async Task<ReaderLookupResult<WikipediaSummaryResponse>> FetchAsync(
        string term, string language, CancellationToken cancellationToken) {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var uri = $"https://{language}.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(term)}";

        try {
            using var response = await client.GetAsync(uri, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) {
                return ReaderLookupResult<WikipediaSummaryResponse>.NotFound();
            }

            if (!response.IsSuccessStatusCode) {
                return ReaderLookupResult<WikipediaSummaryResponse>.Failed(
                    $"Wikipedia returned {(int)response.StatusCode}.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<WikipediaSummaryPayload>(
                stream, JsonOptions, cancellationToken);
            if (payload is null || string.IsNullOrWhiteSpace(payload.Extract)) {
                return ReaderLookupResult<WikipediaSummaryResponse>.NotFound();
            }

            var url = payload.ContentUrls?.Desktop?.Page
                ?? $"https://{language}.wikipedia.org/wiki/{Uri.EscapeDataString(term)}";
            return ReaderLookupResult<WikipediaSummaryResponse>.Ok(new WikipediaSummaryResponse(
                string.IsNullOrWhiteSpace(payload.Title) ? term : payload.Title,
                HtmlTextStripper.StripToText(payload.Extract),
                url,
                payload.Thumbnail?.Source));
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            return ReaderLookupResult<WikipediaSummaryResponse>.Failed("The Wikipedia request timed out.");
        } catch (HttpRequestException ex) {
            _logger.LogDebug(ex, "Wikipedia request failed for term '{Term}'.", term);
            return ReaderLookupResult<WikipediaSummaryResponse>.Failed("Wikipedia could not be reached.");
        } catch (JsonException ex) {
            _logger.LogDebug(ex, "Wikipedia response for term '{Term}' could not be parsed.", term);
            return ReaderLookupResult<WikipediaSummaryResponse>.Failed("Wikipedia's response could not be parsed.");
        }
    }

    private sealed class WikipediaSummaryPayload {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("extract")]
        public string? Extract { get; set; }

        [JsonPropertyName("thumbnail")]
        public WikipediaThumbnail? Thumbnail { get; set; }

        [JsonPropertyName("content_urls")]
        public WikipediaContentUrls? ContentUrls { get; set; }
    }

    private sealed class WikipediaThumbnail {
        [JsonPropertyName("source")]
        public string? Source { get; set; }
    }

    private sealed class WikipediaContentUrls {
        [JsonPropertyName("desktop")]
        public WikipediaDesktopUrl? Desktop { get; set; }
    }

    private sealed class WikipediaDesktopUrl {
        [JsonPropertyName("page")]
        public string? Page { get; set; }
    }
}

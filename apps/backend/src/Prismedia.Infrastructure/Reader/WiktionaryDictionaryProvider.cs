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
/// Default <see cref="IDictionaryProvider"/>: English Wiktionary's REST definition API, grouped by
/// the requested language's ISO code. Results are cached in memory for an hour and HTML markup in
/// the response is stripped down to plain text.
/// </summary>
public sealed class WiktionaryDictionaryProvider : IDictionaryProvider {
    /// <summary>Named <see cref="HttpClient"/> registered for Wiktionary calls.</summary>
    public const string HttpClientName = "PrismediaReaderWiktionary";

    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<WiktionaryDictionaryProvider> _logger;

    public WiktionaryDictionaryProvider(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<WiktionaryDictionaryProvider> logger) {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ReaderLookupResult<DictionaryDefinitionResponse>> DefineAsync(
        string term, string language, CancellationToken cancellationToken) {
        var cacheKey = $"reader:define:{language}:{term.ToLowerInvariant()}";
        if (_cache.TryGetValue(cacheKey, out ReaderLookupResult<DictionaryDefinitionResponse> cached)) {
            return cached;
        }

        var result = await FetchAsync(term, language, cancellationToken);
        // A clean not-found is worth caching too (avoids hammering Wiktionary for a typo'd word
        // re-selected repeatedly); a provider failure is not cached so a transient outage recovers.
        if (!result.ProviderFailed) {
            _cache.Set(cacheKey, result, CacheDuration);
        }

        return result;
    }

    private async Task<ReaderLookupResult<DictionaryDefinitionResponse>> FetchAsync(
        string term, string language, CancellationToken cancellationToken) {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var host = $"{language}.wiktionary.org";
        var uri = $"https://{host}/api/rest_v1/page/definition/{Uri.EscapeDataString(term)}";

        try {
            using var response = await client.GetAsync(uri, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) {
                return ReaderLookupResult<DictionaryDefinitionResponse>.NotFound();
            }

            if (!response.IsSuccessStatusCode) {
                return ReaderLookupResult<DictionaryDefinitionResponse>.Failed(
                    $"Wiktionary returned {(int)response.StatusCode}.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<Dictionary<string, List<WiktionaryEntry>>>(
                stream, JsonOptions, cancellationToken);
            if (payload is null) {
                return ReaderLookupResult<DictionaryDefinitionResponse>.Failed("Wiktionary response was empty.");
            }

            if (!payload.TryGetValue(language, out var rawEntries) || rawEntries.Count == 0) {
                return ReaderLookupResult<DictionaryDefinitionResponse>.NotFound();
            }

            var entries = rawEntries
                .Select(entry => new DictionaryEntry(
                    string.IsNullOrWhiteSpace(entry.PartOfSpeech) ? null : entry.PartOfSpeech,
                    (entry.Definitions ?? [])
                        .Select(definition => new DictionaryDefinition(
                            HtmlTextStripper.StripToText(definition.Definition),
                            (definition.ParsedExamples ?? [])
                                .Select(example => HtmlTextStripper.StripToText(example.Example))
                                .Where(text => text.Length > 0)
                                .ToArray()))
                        .Where(definition => definition.Text.Length > 0)
                        .ToArray()))
                .Where(entry => entry.Definitions.Count > 0)
                .ToArray();

            if (entries.Length == 0) {
                return ReaderLookupResult<DictionaryDefinitionResponse>.NotFound();
            }

            var sourceUrl = $"https://{host}/wiki/{Uri.EscapeDataString(term)}";
            return ReaderLookupResult<DictionaryDefinitionResponse>.Ok(new DictionaryDefinitionResponse(
                term, language, entries, sourceUrl, ReaderProviderKind.Wiktionary, "Wiktionary, CC BY-SA"));
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            return ReaderLookupResult<DictionaryDefinitionResponse>.Failed("The Wiktionary request timed out.");
        } catch (HttpRequestException ex) {
            _logger.LogDebug(ex, "Wiktionary request failed for term '{Term}'.", term);
            return ReaderLookupResult<DictionaryDefinitionResponse>.Failed("Wiktionary could not be reached.");
        } catch (JsonException ex) {
            _logger.LogDebug(ex, "Wiktionary response for term '{Term}' could not be parsed.", term);
            return ReaderLookupResult<DictionaryDefinitionResponse>.Failed("Wiktionary's response could not be parsed.");
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed class WiktionaryEntry {
        [JsonPropertyName("partOfSpeech")]
        public string? PartOfSpeech { get; set; }

        [JsonPropertyName("definitions")]
        public List<WiktionaryDefinition>? Definitions { get; set; }
    }

    private sealed class WiktionaryDefinition {
        [JsonPropertyName("definition")]
        public string? Definition { get; set; }

        [JsonPropertyName("parsedExamples")]
        public List<WiktionaryExample>? ParsedExamples { get; set; }
    }

    private sealed class WiktionaryExample {
        [JsonPropertyName("example")]
        public string? Example { get; set; }
    }
}

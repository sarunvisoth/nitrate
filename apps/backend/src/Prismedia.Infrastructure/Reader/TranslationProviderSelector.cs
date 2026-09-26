using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Prismedia.Application.Reader;
using Prismedia.Application.Settings;
using Prismedia.Contracts.Reader;

namespace Prismedia.Infrastructure.Reader;

/// <summary>
/// The <see cref="ITranslationProvider"/> registered for DI: reads
/// <see cref="AppSettings.Reader"/> per call and delegates to
/// <see cref="LibreTranslateTranslationProvider"/> when a base URL is configured, otherwise to
/// <see cref="MyMemoryTranslationProvider"/>. This keeps the choice a runtime setting rather than a
/// startup-time DI decision, so changing it in Settings takes effect immediately.
/// </summary>
public sealed class TranslationProviderSelector : ITranslationProvider {
    private readonly SettingsService _settings;
    private readonly MyMemoryTranslationProvider _myMemory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILoggerFactory _loggerFactory;

    public TranslationProviderSelector(
        SettingsService settings,
        MyMemoryTranslationProvider myMemory,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILoggerFactory loggerFactory) {
        _settings = settings;
        _myMemory = myMemory;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _loggerFactory = loggerFactory;
    }

    public async Task<ReaderLookupResult<TranslateTextResponse>> TranslateAsync(
        string text, string? sourceLanguage, string targetLanguage, CancellationToken cancellationToken) {
        var values = await _settings.GetValuesAsync(
            [AppSettings.Reader.TranslationLibreUrl.Key, AppSettings.Reader.TranslationLibreApiKey.Key],
            cancellationToken);
        var baseUrl = AppSettings.Reader.TranslationLibreUrl.Read(values.Values[AppSettings.Reader.TranslationLibreUrl.Key]);
        if (string.IsNullOrWhiteSpace(baseUrl)) {
            return await _myMemory.TranslateAsync(text, sourceLanguage, targetLanguage, cancellationToken);
        }

        var apiKey = AppSettings.Reader.TranslationLibreApiKey.Read(
            values.Values[AppSettings.Reader.TranslationLibreApiKey.Key]);
        var provider = new LibreTranslateTranslationProvider(
            _httpClientFactory,
            _cache,
            _loggerFactory.CreateLogger<LibreTranslateTranslationProvider>(),
            baseUrl,
            apiKey);
        return await provider.TranslateAsync(text, sourceLanguage, targetLanguage, cancellationToken);
    }
}

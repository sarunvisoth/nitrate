using Prismedia.Application.Reader;
using Prismedia.Contracts.Reader;
using Prismedia.Domain.Entities;

namespace Prismedia.Application.Tests.Reader;

public sealed class ReaderLookupServiceTests {
    [Fact]
    public async Task DefineRejectsAnEmptyTerm() {
        var service = NewService();

        var outcome = await service.DefineAsync("   ", "en", CancellationToken.None);

        Assert.Equal(ReaderLookupStatus.Invalid, outcome.Status);
    }

    [Fact]
    public async Task DefineDefaultsTheLanguageToEnglishAndLowercasesIt() {
        var dictionary = new RecordingDictionaryProvider();
        var service = NewService(dictionary: dictionary);

        await service.DefineAsync("Ubiquitous", "  EN ", CancellationToken.None);

        Assert.Equal("Ubiquitous", dictionary.LastTerm);
        Assert.Equal("en", dictionary.LastLanguage);
    }

    [Fact]
    public async Task DefineMapsNotFoundAndProviderFailure() {
        var notFound = NewService(dictionary: new RecordingDictionaryProvider {
            Result = ReaderLookupResult<DictionaryDefinitionResponse>.NotFound()
        });
        var failed = NewService(dictionary: new RecordingDictionaryProvider {
            Result = ReaderLookupResult<DictionaryDefinitionResponse>.Failed("boom")
        });

        Assert.Equal(ReaderLookupStatus.NotFound, (await notFound.DefineAsync("x", "en", CancellationToken.None)).Status);
        var unavailable = await failed.DefineAsync("x", "en", CancellationToken.None);
        Assert.Equal(ReaderLookupStatus.ProviderUnavailable, unavailable.Status);
        Assert.Equal("boom", unavailable.Error);
    }

    [Fact]
    public async Task TranslateRejectsMissingText() {
        var service = NewService();

        var outcome = await service.TranslateAsync(new TranslateTextRequest("", null, "fr"), CancellationToken.None);

        Assert.Equal(ReaderLookupStatus.Invalid, outcome.Status);
    }

    [Fact]
    public async Task TranslateRejectsTextOverTheLimit() {
        var service = NewService();
        var text = new string('a', ReaderLookupService.MaxTranslationLength + 1);

        var outcome = await service.TranslateAsync(new TranslateTextRequest(text, null, "fr"), CancellationToken.None);

        Assert.Equal(ReaderLookupStatus.Invalid, outcome.Status);
    }

    [Fact]
    public async Task TranslateRejectsAMissingTargetLanguage() {
        var service = NewService();

        var outcome = await service.TranslateAsync(new TranslateTextRequest("hello", "en", " "), CancellationToken.None);

        Assert.Equal(ReaderLookupStatus.Invalid, outcome.Status);
    }

    [Fact]
    public async Task TranslateAcceptsTextAtExactlyTheLimit() {
        var translation = new RecordingTranslationProvider();
        var service = NewService(translation: translation);
        var text = new string('a', ReaderLookupService.MaxTranslationLength);

        var outcome = await service.TranslateAsync(new TranslateTextRequest(text, null, "fr"), CancellationToken.None);

        Assert.Equal(ReaderLookupStatus.Ok, outcome.Status);
    }

    [Fact]
    public async Task WikipediaRejectsAnEmptyTerm() {
        var service = NewService();

        var outcome = await service.WikipediaAsync(null, "en", CancellationToken.None);

        Assert.Equal(ReaderLookupStatus.Invalid, outcome.Status);
    }

    private static ReaderLookupService NewService(
        IDictionaryProvider? dictionary = null,
        ITranslationProvider? translation = null,
        IEncyclopediaProvider? encyclopedia = null) =>
        new(
            dictionary ?? new RecordingDictionaryProvider(),
            translation ?? new RecordingTranslationProvider(),
            encyclopedia ?? new RecordingEncyclopediaProvider());

    private sealed class RecordingDictionaryProvider : IDictionaryProvider {
        public ReaderLookupResult<DictionaryDefinitionResponse> Result { get; init; } =
            ReaderLookupResult<DictionaryDefinitionResponse>.Ok(new DictionaryDefinitionResponse(
                "x", "en", [], "https://example.test", ReaderProviderKind.Wiktionary, "Wiktionary, CC BY-SA"));
        public string? LastTerm { get; private set; }
        public string? LastLanguage { get; private set; }

        public Task<ReaderLookupResult<DictionaryDefinitionResponse>> DefineAsync(
            string term, string language, CancellationToken cancellationToken) {
            LastTerm = term;
            LastLanguage = language;
            return Task.FromResult(Result);
        }
    }

    private sealed class RecordingTranslationProvider : ITranslationProvider {
        public ReaderLookupResult<TranslateTextResponse> Result { get; init; } =
            ReaderLookupResult<TranslateTextResponse>.Ok(new TranslateTextResponse(
                "x", "y", "en", "fr", ReaderProviderKind.MyMemory));

        public Task<ReaderLookupResult<TranslateTextResponse>> TranslateAsync(
            string text, string? sourceLanguage, string targetLanguage, CancellationToken cancellationToken) =>
            Task.FromResult(Result);
    }

    private sealed class RecordingEncyclopediaProvider : IEncyclopediaProvider {
        public ReaderLookupResult<WikipediaSummaryResponse> Result { get; init; } =
            ReaderLookupResult<WikipediaSummaryResponse>.Ok(new WikipediaSummaryResponse(
                "x", "y", "https://example.test", null));

        public Task<ReaderLookupResult<WikipediaSummaryResponse>> SummarizeAsync(
            string term, string language, CancellationToken cancellationToken) =>
            Task.FromResult(Result);
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Prismedia.Application.Reader;
using Prismedia.Contracts.Reader;
using Prismedia.Contracts.System;
using Prismedia.Domain.Entities;

namespace Prismedia.Api.Tests;

public sealed class ReaderEndpointsTests {
    [Fact]
    public async Task DefineRequiresAuthentication() {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/reader/define?term=ubiquitous");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DefineReturnsEntriesFromTheProvider() {
        var dictionary = new FakeDictionaryProvider {
            Result = ReaderLookupResult<DictionaryDefinitionResponse>.Ok(new DictionaryDefinitionResponse(
                "ubiquitous", "en",
                [new DictionaryEntry("adjective", [new DictionaryDefinition("present everywhere", [])])],
                "https://en.wiktionary.org/wiki/ubiquitous",
                ReaderProviderKind.Wiktionary, "Wiktionary, CC BY-SA"))
        };
        using var factory = CreateFactory(dictionary: dictionary);
        using var client = factory.CreateAuthenticatedClient();

        using var response = await client.GetAsync("/api/reader/define?term=ubiquitous&language=en");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ubiquitous", body.RootElement.GetProperty("term").GetString());
        Assert.Equal("adjective", body.RootElement.GetProperty("entries")[0].GetProperty("partOfSpeech").GetString());
        Assert.Equal("wiktionary", body.RootElement.GetProperty("provider").GetString());
        Assert.Equal("ubiquitous", dictionary.LastTerm);
        Assert.Equal("en", dictionary.LastLanguage);
    }

    [Fact]
    public async Task DefineWithoutATermIsABadRequest() {
        using var factory = CreateFactory();
        using var client = factory.CreateAuthenticatedClient();

        using var response = await client.GetAsync("/api/reader/define");
        var problem = await response.Content.ReadFromJsonAsync<ApiProblem>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ApiProblemCodes.ReaderLookupInvalid, problem!.Code);
    }

    [Fact]
    public async Task DefineOfAnUnknownTermIsNotFound() {
        using var factory = CreateFactory(dictionary: new FakeDictionaryProvider {
            Result = ReaderLookupResult<DictionaryDefinitionResponse>.NotFound()
        });
        using var client = factory.CreateAuthenticatedClient();

        using var response = await client.GetAsync("/api/reader/define?term=zzzznotaword");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DefineMapsProviderFailureToBadGateway() {
        using var factory = CreateFactory(dictionary: new FakeDictionaryProvider {
            Result = ReaderLookupResult<DictionaryDefinitionResponse>.Failed("Wiktionary could not be reached.")
        });
        using var client = factory.CreateAuthenticatedClient();

        using var response = await client.GetAsync("/api/reader/define?term=ubiquitous");
        var problem = await response.Content.ReadFromJsonAsync<ApiProblem>();

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(ApiProblemCodes.ReaderLookupProviderUnavailable, problem!.Code);
    }

    [Fact]
    public async Task TranslateRejectsTextOverTheLengthLimit() {
        using var factory = CreateFactory();
        using var client = factory.CreateAuthenticatedClient();

        using var response = await client.PostAsJsonAsync(
            "/api/reader/translate",
            new TranslateTextRequest(new string('a', 501), null, "fr"));
        var problem = await response.Content.ReadFromJsonAsync<ApiProblem>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ApiProblemCodes.ReaderLookupInvalid, problem!.Code);
    }

    [Fact]
    public async Task TranslateReturnsTheProviderResult() {
        var translation = new FakeTranslationProvider {
            Result = ReaderLookupResult<TranslateTextResponse>.Ok(new TranslateTextResponse(
                "hello", "bonjour", "en", "fr", ReaderProviderKind.MyMemory))
        };
        using var factory = CreateFactory(translation: translation);
        using var client = factory.CreateAuthenticatedClient();

        using var response = await client.PostAsJsonAsync(
            "/api/reader/translate", new TranslateTextRequest("hello", "en", "fr"));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("bonjour", body.RootElement.GetProperty("translatedText").GetString());
    }

    [Fact]
    public async Task WikipediaReturnsTheProviderResult() {
        var encyclopedia = new FakeEncyclopediaProvider {
            Result = ReaderLookupResult<WikipediaSummaryResponse>.Ok(new WikipediaSummaryResponse(
                "Ubiquitous", "found everywhere", "https://en.wikipedia.org/wiki/Ubiquitous", null))
        };
        using var factory = CreateFactory(encyclopedia: encyclopedia);
        using var client = factory.CreateAuthenticatedClient();

        using var response = await client.GetAsync("/api/reader/wikipedia?term=ubiquitous");
        var body = await response.Content.ReadFromJsonAsync<WikipediaSummaryResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Ubiquitous", body!.Title);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        IDictionaryProvider? dictionary = null,
        ITranslationProvider? translation = null,
        IEncyclopediaProvider? encyclopedia = null) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => {
                builder.ConfigureServices(services => {
                    services.RemoveAll<IDictionaryProvider>();
                    services.AddSingleton(dictionary ?? new FakeDictionaryProvider());
                    services.RemoveAll<ITranslationProvider>();
                    services.AddSingleton(translation ?? new FakeTranslationProvider());
                    services.RemoveAll<IEncyclopediaProvider>();
                    services.AddSingleton(encyclopedia ?? new FakeEncyclopediaProvider());
                });
            })
            .WithTestAuth();

    private sealed class FakeDictionaryProvider : IDictionaryProvider {
        public ReaderLookupResult<DictionaryDefinitionResponse> Result { get; init; } =
            ReaderLookupResult<DictionaryDefinitionResponse>.NotFound();
        public string? LastTerm { get; private set; }
        public string? LastLanguage { get; private set; }

        public Task<ReaderLookupResult<DictionaryDefinitionResponse>> DefineAsync(
            string term, string language, CancellationToken cancellationToken) {
            LastTerm = term;
            LastLanguage = language;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeTranslationProvider : ITranslationProvider {
        public ReaderLookupResult<TranslateTextResponse> Result { get; init; } =
            ReaderLookupResult<TranslateTextResponse>.NotFound();

        public Task<ReaderLookupResult<TranslateTextResponse>> TranslateAsync(
            string text, string? sourceLanguage, string targetLanguage, CancellationToken cancellationToken) =>
            Task.FromResult(Result);
    }

    private sealed class FakeEncyclopediaProvider : IEncyclopediaProvider {
        public ReaderLookupResult<WikipediaSummaryResponse> Result { get; init; } =
            ReaderLookupResult<WikipediaSummaryResponse>.NotFound();

        public Task<ReaderLookupResult<WikipediaSummaryResponse>> SummarizeAsync(
            string term, string language, CancellationToken cancellationToken) =>
            Task.FromResult(Result);
    }
}

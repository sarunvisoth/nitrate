import { render, screen } from "@testing-library/svelte";
import { afterEach, describe, expect, it, vi } from "vitest";
import LookupPopover from "./LookupPopover.svelte";
import { saveVocabularyEntry } from "$lib/api/vocabulary";

const anchor = { left: 10, top: 20, width: 40, height: 16 };

vi.mock("$lib/api/reader-lookups", () => ({
  defineTerm: vi.fn().mockResolvedValue({
    status: "ok",
    data: {
      term: "luminous",
      language: "en",
      entries: [{ partOfSpeech: "adjective", definitions: [{ text: "Giving off light; bright.", examples: [] }] }],
      sourceUrl: "https://en.wiktionary.org/wiki/luminous",
      provider: "wiktionary",
      attribution: "Wiktionary, CC BY-SA",
    },
  }),
  translateText: vi.fn(),
  wikipediaSummary: vi.fn(),
}));

vi.mock("$lib/api/vocabulary", () => ({
  saveVocabularyEntry: vi.fn().mockResolvedValue({}),
}));

afterEach(() => vi.clearAllMocks());

describe("LookupPopover Add to Vocabulary", () => {
  it("saves the word with its book id, CFI locator, and surrounding sentence", async () => {
    render(LookupPopover, {
      props: {
        anchor,
        term: "luminous",
        initialMode: "define",
        onDismiss: vi.fn(),
        sourceContext: {
          bookId: "book-1",
          locator: "epubcfi(/6/4!/4/2,/1:0,/1:5)",
          textBefore: "The dawn was ",
          textAfter: " over the hills.",
        },
      },
    });

    const addButton = await screen.findByRole("button", { name: "Add to Vocabulary" });
    addButton.click();

    await screen.findByRole("button", { name: "In vocabulary" });

    expect(saveVocabularyEntry).toHaveBeenCalledWith(
      expect.any(String),
      expect.objectContaining({
        word: "luminous",
        definition: "Giving off light; bright.",
        bookId: "book-1",
        sourceLocator: "epubcfi(/6/4!/4/2,/1:0,/1:5)",
        sourceSentence: "The dawn was luminous over the hills.",
      }),
    );
  });

  it("saves without book/locator/sentence when there is no source context", async () => {
    render(LookupPopover, {
      props: { anchor, term: "luminous", initialMode: "define", onDismiss: vi.fn(), sourceContext: null },
    });

    const addButton = await screen.findByRole("button", { name: "Add to Vocabulary" });
    addButton.click();
    await screen.findByRole("button", { name: "In vocabulary" });

    expect(saveVocabularyEntry).toHaveBeenCalledWith(
      expect.any(String),
      expect.objectContaining({ bookId: null, sourceLocator: null, sourceSentence: null }),
    );
  });
});

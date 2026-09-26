import { describe, expect, it } from "vitest";
import type { ReaderSearchResponse } from "$lib/api/generated/model";
import { searchReaderExtras } from "./reader-search";

const emptyResponse: ReaderSearchResponse = { annotations: [], vocabulary: [] };

describe("searchReaderExtras", () => {
  it("returns nothing for a blank or too-short query without calling the fetcher", async () => {
    let called = false;
    const result = await searchReaderExtras(" a ", 5, async () => {
      called = true;
      return emptyResponse;
    });

    expect(result).toEqual([]);
    expect(called).toBe(false);
  });

  it("builds an annotation group with a reader link and a see-all link", async () => {
    const response: ReaderSearchResponse = {
      annotations: [
        {
          id: "ann-1",
          bookId: "book-1",
          bookTitle: "Dune",
          locatorKind: "epub-cfi",
          locator: "/6/4",
          spineIndex: 0,
          position: 0.1,
          selectedText: "the spice must flow",
          note: null,
          style: "highlight",
          color: "yellow",
          isStale: false,
          createdAt: "2026-01-01T00:00:00Z",
          updatedAt: "2026-01-01T00:00:00Z",
        },
      ],
      vocabulary: [],
    };

    const groups = await searchReaderExtras("spice", 5, async () => response);

    expect(groups).toHaveLength(1);
    expect(groups[0].kind).toBe("annotation");
    expect(groups[0].seeAllHref).toBe("/annotations?q=spice");
    expect(groups[0].items[0]).toMatchObject({
      title: "Dune",
      snippet: "the spice must flow",
      meta: "yellow",
      href: "/books/book-1/reader?kind=book&id=book-1&location=%2F6%2F4",
    });
  });

  it("builds a vocabulary group and links to /vocabulary when there is no source locator", async () => {
    const response: ReaderSearchResponse = {
      annotations: [],
      vocabulary: [
        {
          id: "voc-1",
          word: "ephemeral",
          language: "en",
          definition: "lasting a short time",
          bookId: null,
          bookTitle: null,
          sourceLocator: null,
          sourceSentence: null,
          status: "learning",
          createdAt: "2026-01-01T00:00:00Z",
          updatedAt: "2026-01-01T00:00:00Z",
        },
      ],
    };

    const groups = await searchReaderExtras("ephem", 5, async () => response);

    expect(groups).toHaveLength(1);
    expect(groups[0].kind).toBe("vocabulary");
    expect(groups[0].items[0]).toMatchObject({
      title: "ephemeral",
      snippet: "lasting a short time",
      meta: "learning",
      href: "/vocabulary?q=ephemeral",
    });
  });

  it("truncates long snippets", async () => {
    const longText = "x".repeat(200);
    const response: ReaderSearchResponse = {
      annotations: [
        {
          id: "ann-1",
          bookId: "book-1",
          bookTitle: "Dune",
          locatorKind: "epub-cfi",
          locator: "/6/4",
          spineIndex: 0,
          position: 0.1,
          selectedText: longText,
          note: null,
          style: "highlight",
          color: "yellow",
          isStale: false,
          createdAt: "2026-01-01T00:00:00Z",
          updatedAt: "2026-01-01T00:00:00Z",
        },
      ],
      vocabulary: [],
    };

    const groups = await searchReaderExtras("xx", 5, async () => response);

    expect(groups[0].items[0].snippet.length).toBeLessThanOrEqual(160);
    expect(groups[0].items[0].snippet.endsWith("…")).toBe(true);
  });
});

import { describe, expect, it } from "vitest";
import { PdfTextCache, searchPdfDocument, type PdfLikeDocument } from "./pdf-text-search";

function fakePdf(pages: string[]): PdfLikeDocument & { fetchCount: number[] } {
  const fetchCount = pages.map(() => 0);
  return {
    numPages: pages.length,
    fetchCount,
    async getPage(pageNumber: number) {
      const index = pageNumber - 1;
      fetchCount[index]++;
      return {
        async getTextContent() {
          return { items: pages[index].split(" ").map((str) => ({ str })) };
        },
      };
    },
  };
}

async function collect<T>(iterable: AsyncIterable<T>): Promise<T[]> {
  const out: T[] = [];
  for await (const item of iterable) out.push(item);
  return out;
}

describe("searchPdfDocument", () => {
  it("finds matches across pages, case-insensitively", async () => {
    const pdf = fakePdf(["The quick brown fox", "jumps over the Lazy dog", "no match here"]);
    const cache = new PdfTextCache(pdf);

    const matches = await collect(searchPdfDocument(pdf, "lazy", cache));

    expect(matches).toEqual([{ page: 1, excerpt: { pre: "jumps over the", match: "Lazy", post: "dog" } }]);
  });

  it("finds every occurrence on the same page", async () => {
    const pdf = fakePdf(["cat cat cat"]);
    const cache = new PdfTextCache(pdf);

    const matches = await collect(searchPdfDocument(pdf, "cat", cache));

    expect(matches.map((m) => m.page)).toEqual([0, 0, 0]);
  });

  it("returns nothing for a blank query", async () => {
    const pdf = fakePdf(["some text"]);
    const cache = new PdfTextCache(pdf);

    expect(await collect(searchPdfDocument(pdf, "   ", cache))).toEqual([]);
  });

  it("stops fetching page text once the caller stops iterating", async () => {
    const pdf = fakePdf(["alpha", "alpha", "alpha"]);
    const cache = new PdfTextCache(pdf);

    for await (const _match of searchPdfDocument(pdf, "alpha", cache)) {
      break;
    }

    expect(pdf.fetchCount).toEqual([1, 0, 0]);
  });
});

describe("PdfTextCache", () => {
  it("fetches each page's text only once", async () => {
    const pdf = fakePdf(["one", "two"]);
    const cache = new PdfTextCache(pdf);

    await cache.textFor(0);
    await cache.textFor(0);
    await cache.textFor(1);

    expect(pdf.fetchCount).toEqual([1, 1]);
  });

  it("caches an empty string when the page fails to load", async () => {
    const pdf: PdfLikeDocument = {
      numPages: 1,
      getPage: () => Promise.reject(new Error("boom")),
    };
    const cache = new PdfTextCache(pdf);

    await expect(cache.textFor(0)).resolves.toBe("");
  });
});

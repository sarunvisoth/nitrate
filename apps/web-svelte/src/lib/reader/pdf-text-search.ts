/** One match of a search query within the document. */
export interface PdfSearchMatch {
  page: number;
  excerpt: { pre: string; match: string; post: string };
}

/** Loose shape of a pdf.js document proxy, enough for search (avoids a hard dependency on pdfjs-dist's types). */
export interface PdfLikeDocument {
  numPages: number;
  getPage(pageNumber: number): Promise<PdfLikePage>;
}

export interface PdfLikePage {
  getTextContent(): Promise<{ items: ReadonlyArray<{ str?: unknown }> }>;
}

const EXCERPT_CONTEXT_LENGTH = 48;

/** Per-document cache of each page's plain text, built lazily (one `getTextContent()` call per page, once). */
export class PdfTextCache {
  #pages: (string | undefined)[];

  constructor(private readonly pdf: PdfLikeDocument) {
    this.#pages = new Array(pdf.numPages);
  }

  /** The page's text (0-based index), fetching and caching it on first use. */
  async textFor(pageIndex: number): Promise<string> {
    const cached = this.#pages[pageIndex];
    if (cached !== undefined) return cached;
    let text = "";
    try {
      const page = await this.pdf.getPage(pageIndex + 1);
      const content = await page.getTextContent();
      text = content.items.map((item) => (typeof item.str === "string" ? item.str : "")).join(" ");
    } catch {
      text = "";
    }
    this.#pages[pageIndex] = text;
    return text;
  }
}

/**
 * Searches every page of the document for `query` (case-insensitive), yielding matches page by page as they are
 * found. Cancellable: the caller stops iterating (e.g. `break`, or a superseding call checks its own token
 * before consuming further) and no further page text is fetched.
 */
export async function* searchPdfDocument(
  pdf: PdfLikeDocument,
  query: string,
  cache: PdfTextCache,
): AsyncGenerator<PdfSearchMatch> {
  const needle = query.trim().toLowerCase();
  if (!needle) return;

  for (let page = 0; page < pdf.numPages; page++) {
    const text = await cache.textFor(page);
    const lower = text.toLowerCase();
    let from = lower.indexOf(needle);
    while (from !== -1) {
      yield { page, excerpt: buildExcerpt(text, from, needle.length) };
      from = lower.indexOf(needle, from + needle.length);
    }
  }
}

function buildExcerpt(text: string, matchStart: number, matchLength: number): PdfSearchMatch["excerpt"] {
  const matchEnd = matchStart + matchLength;
  return {
    pre: text.slice(Math.max(0, matchStart - EXCERPT_CONTEXT_LENGTH), matchStart).trim(),
    match: text.slice(matchStart, matchEnd),
    post: text.slice(matchEnd, matchEnd + EXCERPT_CONTEXT_LENGTH).trim(),
  };
}

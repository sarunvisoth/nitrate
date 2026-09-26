import { afterEach, describe, expect, it, vi } from "vitest";
import { ANNOTATION_COLOR, ANNOTATION_EXPORT_FORMAT } from "$lib/api/generated/codes";
import { annotationExportUrl, fetchAnnotationLibrary, fetchAnnotationLibraryBooks } from "./annotation-library";

describe("annotation library API", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("sends only the filters that are set", async () => {
    const fetchMock = vi.fn(async () => new Response(JSON.stringify({ items: [], nextCursor: null, totalCount: 0, books: [] }), {
      headers: { "content-type": "application/json" },
    }));
    vi.stubGlobal("fetch", fetchMock);

    await fetchAnnotationLibrary({ search: "noise", color: ANNOTATION_COLOR.blue, notesOnly: true });
    await fetchAnnotationLibrary({});
    await fetchAnnotationLibraryBooks();

    const urls = (fetchMock.mock.calls as unknown as Array<[string]>).map(([url]) => url);
    expect(urls[0]).toBe("/api/annotations?search=noise&color=blue&notesOnly=true");
    expect(urls[1]).toBe("/api/annotations");
    expect(urls[2]).toBe("/api/annotations/books");
  });

  it("builds same-origin export download links", () => {
    expect(annotationExportUrl(ANNOTATION_EXPORT_FORMAT.csv)).toBe("/api/annotations/export?format=csv");
    expect(annotationExportUrl(ANNOTATION_EXPORT_FORMAT.markdown, "b1")).toBe("/api/annotations/export?format=markdown&bookId=b1");
  });
});

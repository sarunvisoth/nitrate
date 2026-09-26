import { afterEach, describe, expect, it, vi } from "vitest";
import type { BookAnnotation } from "$lib/api/generated/model";
import { ANNOTATION_COLOR, ANNOTATION_LOCATOR_KIND, ANNOTATION_STYLE } from "$lib/api/generated/codes";
import { deleteBookAnnotation, fetchBookAnnotations, saveBookAnnotation } from "./annotations";

const annotation: BookAnnotation = {
  id: "a1",
  bookId: "b1",
  locatorKind: ANNOTATION_LOCATOR_KIND.epubCfi,
  locator: "epubcfi(/6/4!/4/2,/1:0,/1:5)",
  spineIndex: 1,
  position: 0.1,
  selectedText: "Hello",
  textBefore: "Say ",
  textAfter: " world",
  style: ANNOTATION_STYLE.highlight,
  color: ANNOTATION_COLOR.green,
  note: "note",
  sourceSignature: null,
  createdAt: "2026-09-26T10:00:00Z",
  updatedAt: "2026-09-26T10:05:00Z",
  deletedAt: null,
};

describe("annotations API", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("lists a book's annotations", async () => {
    const fetchMock = mockFetch({ annotations: [annotation] });

    const result = await fetchBookAnnotations("b1");

    const [url] = fetchMock.mock.calls[0] as unknown as [string];
    expect(url).toBe("/api/books/b1/annotations");
    expect(result.annotations).toHaveLength(1);
  });

  it("saves with PUT and sends only the writable fields", async () => {
    const fetchMock = mockFetch(annotation);

    await saveBookAnnotation("b1", annotation);

    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    expect(url).toBe("/api/books/b1/annotations/a1");
    expect(init.method).toBe("PUT");
    const body = JSON.parse(String(init.body));
    expect(body).toEqual({
      locatorKind: annotation.locatorKind,
      locator: annotation.locator,
      spineIndex: 1,
      position: 0.1,
      selectedText: "Hello",
      textBefore: "Say ",
      textAfter: " world",
      style: annotation.style,
      color: annotation.color,
      note: "note",
      updatedAt: annotation.updatedAt,
    });
  });

  it("deletes with the client clock in the query", async () => {
    const fetchMock = mockFetch({ ...annotation, deletedAt: "2026-09-26T11:00:00Z" });

    const result = await deleteBookAnnotation("b1", "a1", "2026-09-26T11:00:00Z");

    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    expect(url).toBe("/api/books/b1/annotations/a1?updatedAt=2026-09-26T11%3A00%3A00Z");
    expect(init.method).toBe("DELETE");
    expect(result.deletedAt).toBe("2026-09-26T11:00:00Z");
  });
});

function mockFetch(data: unknown) {
  const fetchMock = vi.fn(async () => new Response(JSON.stringify(data), {
    headers: { "content-type": "application/json" },
    status: 200,
  }));
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

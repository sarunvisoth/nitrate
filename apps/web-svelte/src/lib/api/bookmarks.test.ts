import { afterEach, describe, expect, it, vi } from "vitest";
import type { BookBookmark } from "$lib/api/generated/model";
import { ANNOTATION_LOCATOR_KIND } from "$lib/api/generated/codes";
import { deleteBookBookmark, fetchBookBookmarks, saveBookBookmark } from "./bookmarks";

const bookmark: BookBookmark = {
  id: "m1",
  bookId: "b1",
  locatorKind: ANNOTATION_LOCATOR_KIND.epubCfi,
  locator: "epubcfi(/6/4!/4/2/1:0)",
  spineIndex: 1,
  position: 0.2,
  chapterLabel: "The Harbour",
  label: "Letter",
  createdAt: "2026-09-26T10:00:00Z",
  updatedAt: "2026-09-26T10:05:00Z",
  deletedAt: null,
};

describe("bookmarks API", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("lists, saves writable fields with PUT, and deletes with the clock", async () => {
    const fetchMock = vi.fn(async () => new Response(JSON.stringify({ bookmarks: [bookmark], ...bookmark }), {
      headers: { "content-type": "application/json" },
      status: 200,
    }));
    vi.stubGlobal("fetch", fetchMock);

    await fetchBookBookmarks("b1");
    await saveBookBookmark("b1", bookmark);
    await deleteBookBookmark("b1", "m1", "2026-09-26T11:00:00Z");

    const calls = fetchMock.mock.calls as unknown as Array<[string, RequestInit]>;
    expect(calls[0]![0]).toBe("/api/books/b1/bookmarks");
    expect(calls[1]![0]).toBe("/api/books/b1/bookmarks/m1");
    expect(calls[1]![1].method).toBe("PUT");
    expect(JSON.parse(String(calls[1]![1].body))).toEqual({
      locatorKind: bookmark.locatorKind,
      locator: bookmark.locator,
      spineIndex: 1,
      position: 0.2,
      chapterLabel: "The Harbour",
      label: "Letter",
      updatedAt: bookmark.updatedAt,
    });
    expect(calls[2]![0]).toBe("/api/books/b1/bookmarks/m1?updatedAt=2026-09-26T11%3A00%3A00Z");
    expect(calls[2]![1].method).toBe("DELETE");
  });
});

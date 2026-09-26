import { describe, expect, it, vi } from "vitest";
import type { BookBookmark } from "$lib/api/generated/model";
import { ANNOTATION_LOCATOR_KIND } from "$lib/api/generated/codes";
import { BookBookmarkStore, type BookBookmarkApi } from "./book-bookmark-store.svelte";

function clock(start = Date.parse("2026-09-26T10:00:00Z")) {
  let t = start;
  return () => new Date((t += 1000)).toISOString();
}

function fakeApi() {
  const server = new Map<string, BookBookmark>();
  const api: BookBookmarkApi = {
    list: vi.fn(async () => ({ bookmarks: [...server.values()] })),
    save: vi.fn(async (_bookId, bookmark) => {
      server.set(bookmark.id, bookmark);
      return bookmark;
    }),
    remove: vi.fn(async (_bookId, id, updatedAt) => {
      const stored = { ...server.get(id)!, updatedAt, deletedAt: updatedAt };
      server.set(id, stored);
      return stored;
    }),
  };
  return { api, server };
}

const page = { locator: "epubcfi(/6/4!/4/2/1:0)", spineIndex: 1, position: 0.2, chapterLabel: "The Harbour" };

describe("BookBookmarkStore", () => {
  it("adds a bookmark with the chapter label and saves it", async () => {
    const { api, server } = fakeApi();
    const store = new BookBookmarkStore("b1", api, clock(), () => "m1");

    const created = store.add(page);
    await store.idle();

    expect(created.locatorKind).toBe(ANNOTATION_LOCATOR_KIND.epubCfi);
    expect(store.visible.map((b) => b.chapterLabel)).toEqual(["The Harbour"]);
    expect(server.get("m1")?.label).toBeNull();
  });

  it("renames and deletes", async () => {
    const { api, server } = fakeApi();
    const store = new BookBookmarkStore("b1", api, clock(), () => "m1");
    const created = store.add(page);

    store.rename(created, "  Mara finds the letter ");
    await store.idle();
    expect(server.get("m1")?.label).toBe("Mara finds the letter");

    store.remove(store.visible[0]!);
    await store.idle();
    expect(store.visible).toEqual([]);
    expect(server.get("m1")?.deletedAt).not.toBeNull();
  });

  it("lists visible bookmarks in reading order", async () => {
    const { api } = fakeApi();
    let n = 0;
    const store = new BookBookmarkStore("b1", api, clock(), () => `m${++n}`);
    store.add({ ...page, spineIndex: 2, position: 0.6 });
    store.add({ ...page, spineIndex: 1, position: 0.2 });

    expect(store.visible.map((b) => b.id)).toEqual(["m2", "m1"]);
  });
});

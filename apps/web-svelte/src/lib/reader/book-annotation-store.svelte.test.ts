import { describe, expect, it, vi } from "vitest";
import type { BookAnnotation } from "$lib/api/generated/model";
import { ANNOTATION_COLOR, ANNOTATION_LOCATOR_KIND, ANNOTATION_STYLE } from "$lib/api/generated/codes";
import { BookAnnotationStore, type BookAnnotationApi } from "./book-annotation-store.svelte";

const draft = {
  locator: "epubcfi(/6/4!/4/2,/1:0,/1:5)",
  spineIndex: 1,
  position: 0.1,
  selectedText: "Hello",
  textBefore: null,
  textAfter: null,
  color: ANNOTATION_COLOR.yellow,
  note: null,
};

function clock(start = Date.parse("2026-09-26T10:00:00Z")) {
  let t = start;
  return () => new Date((t += 1000)).toISOString();
}

function fakeApi(overrides: Partial<BookAnnotationApi> = {}) {
  const server = new Map<string, BookAnnotation>();
  const api: BookAnnotationApi = {
    list: vi.fn(async () => ({ annotations: [...server.values()] })),
    save: vi.fn(async (_bookId, annotation) => {
      server.set(annotation.id, annotation);
      return annotation;
    }),
    remove: vi.fn(async (_bookId, id, updatedAt) => {
      const stored = { ...server.get(id)!, updatedAt, deletedAt: updatedAt };
      server.set(id, stored);
      return stored;
    }),
    ...overrides,
  };
  return { api, server };
}

describe("BookAnnotationStore", () => {
  it("applies a new highlight immediately and saves it", async () => {
    const { api, server } = fakeApi();
    const store = new BookAnnotationStore("b1", api, clock(), () => "a1");

    const created = store.create(draft);
    expect(store.visible.map((a) => a.id)).toEqual(["a1"]);
    await store.idle();

    expect(created.bookId).toBe("b1");
    expect(server.get("a1")?.selectedText).toBe("Hello");
  });

  it("updates and deletes with fresh clocks", async () => {
    const { api, server } = fakeApi();
    const store = new BookAnnotationStore("b1", api, clock(), () => "a1");
    const created = store.create(draft);

    store.update({ ...created, color: ANNOTATION_COLOR.blue });
    store.remove(store.visible[0]!);
    await store.idle();

    expect(store.visible).toEqual([]);
    expect(server.get("a1")?.deletedAt).not.toBeNull();
    expect(Date.parse(server.get("a1")!.updatedAt)).toBeGreaterThan(Date.parse(created.updatedAt));
  });

  it("adopts a newer server version returned by a superseded write", async () => {
    const newer = (a: BookAnnotation) => ({ ...a, color: ANNOTATION_COLOR.red, updatedAt: "2030-01-01T00:00:00Z" });
    const { api } = fakeApi({ save: vi.fn(async (_b, a) => newer(a)) });
    const store = new BookAnnotationStore("b1", api, clock(), () => "a1");

    store.create(draft);
    await store.idle();

    expect(store.visible[0]?.color).toBe(ANNOTATION_COLOR.red);
  });

  it("keeps a failed write and resends it on the next refresh", async () => {
    let online = false;
    const { api, server } = fakeApi();
    const save = vi.fn(async (bookId: string, annotation: BookAnnotation) => {
      if (!online) throw new Error("offline");
      server.set(annotation.id, annotation);
      return annotation;
    });
    const store = new BookAnnotationStore("b1", { ...api, save }, clock(), () => "a1");

    store.create(draft);
    await store.idle();
    expect(server.has("a1")).toBe(false);
    expect(store.visible).toHaveLength(1);

    online = true;
    await store.refresh();
    await store.idle();

    expect(server.has("a1")).toBe(true);
  });

  it("merges annotations made on another device during refresh", async () => {
    const { api, server } = fakeApi();
    const store = new BookAnnotationStore("b1", api, clock(), () => "local");
    server.set("remote", {
      id: "remote", bookId: "b1", locatorKind: ANNOTATION_LOCATOR_KIND.epubCfi, locator: "epubcfi(/6/8!/4/2,/1:0,/1:3)",
      spineIndex: 3, position: 0.4, selectedText: "Hey", textBefore: null, textAfter: null,
      style: ANNOTATION_STYLE.highlight, color: ANNOTATION_COLOR.green, note: null, sourceSignature: null,
      createdAt: "2026-09-26T09:00:00Z", updatedAt: "2026-09-26T09:00:00Z", deletedAt: null,
    });

    await store.refresh();

    expect(store.visible.map((a) => a.id)).toEqual(["remote"]);
  });
});

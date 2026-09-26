import { describe, expect, it } from "vitest";
import type { BookAnnotation } from "$lib/api/generated/model";
import { ANNOTATION_COLOR, ANNOTATION_LOCATOR_KIND, ANNOTATION_STYLE } from "$lib/api/generated/codes";
import { mergeAll, mergeAnnotation, visibleAnnotations } from "./annotation-sync";

const base: BookAnnotation = {
  id: "a1",
  bookId: "b1",
  locatorKind: ANNOTATION_LOCATOR_KIND.epubCfi,
  locator: "epubcfi(/6/4!/4/2,/1:0,/1:5)",
  spineIndex: 1,
  position: 0.1,
  selectedText: "Hello",
  textBefore: null,
  textAfter: null,
  style: ANNOTATION_STYLE.highlight,
  color: ANNOTATION_COLOR.yellow,
  note: null,
  sourceSignature: null,
  createdAt: "2026-09-26T10:00:00Z",
  updatedAt: "2026-09-26T10:00:00Z",
  deletedAt: null,
};

describe("annotation-sync", () => {
  it("keeps the newer updatedAt (last write wins)", () => {
    const newer = { ...base, color: ANNOTATION_COLOR.blue, updatedAt: "2026-09-26T11:00:00Z" };
    expect(mergeAnnotation(base, newer).color).toBe(ANNOTATION_COLOR.blue);
    expect(mergeAnnotation(newer, base).color).toBe(ANNOTATION_COLOR.blue);
    expect(mergeAnnotation(undefined, base)).toBe(base);
  });

  it("merges a server list into the local map by id", () => {
    const local = new Map([[base.id, { ...base, updatedAt: "2026-09-26T12:00:00Z", note: "local" }]]);
    const merged = mergeAll(local, [base, { ...base, id: "a2" }]);
    expect(merged.get("a1")?.note).toBe("local");
    expect(merged.has("a2")).toBe(true);
  });

  it("hides tombstones and sorts in reading order", () => {
    const later = { ...base, id: "a3", spineIndex: "2", position: "0.5" };
    const earlier = { ...base, id: "a4", spineIndex: 1, position: 0.05 };
    const deleted = { ...base, id: "a2", deletedAt: "2026-09-26T12:00:00Z" };
    expect(visibleAnnotations([later, base, deleted, earlier]).map((a) => a.id)).toEqual(["a4", "a1", "a3"]);
  });
});

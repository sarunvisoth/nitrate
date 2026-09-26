import { describe, expect, it } from "vitest";
import { buildPdfLocator, parsePdfLocator, pdfPageLabel, PDF_LOCATOR_MAX_RECTS } from "./pdf-locator";

describe("buildPdfLocator / parsePdfLocator", () => {
  it("round-trips a page + rects locator", () => {
    const locator = { page: 3, rects: [{ x: 0.1, y: 0.2, w: 0.3, h: 0.04 }] };
    expect(parsePdfLocator(buildPdfLocator(locator))).toEqual(locator);
  });

  it("accepts a page bookmark with no rects", () => {
    expect(parsePdfLocator(buildPdfLocator({ page: 0, rects: [] }))).toEqual({ page: 0, rects: [] });
  });

  it.each([
    ["malformed JSON", "not json"],
    ["a JSON scalar", "42"],
    ["a negative page", JSON.stringify({ page: -1, rects: [] })],
    ["a missing page", JSON.stringify({ rects: [] })],
    ["an out-of-range rect x", JSON.stringify({ page: 0, rects: [{ x: 1.5, y: 0, w: 0.1, h: 0.1 }] })],
    ["a negative rect coordinate", JSON.stringify({ page: 0, rects: [{ x: 0, y: -0.1, w: 0.1, h: 0.1 }] })],
    ["a non-numeric rect field", JSON.stringify({ page: 0, rects: [{ x: "0", y: 0, w: 0.1, h: 0.1 }] })],
  ])("rejects %s", (_label, json) => {
    expect(parsePdfLocator(json)).toBeNull();
  });

  it("rejects more than the maximum number of rects", () => {
    const rects = Array.from({ length: PDF_LOCATOR_MAX_RECTS + 1 }, () => ({ x: 0, y: 0, w: 0.1, h: 0.1 }));
    expect(parsePdfLocator(buildPdfLocator({ page: 0, rects }))).toBeNull();
  });

  it("accepts exactly the maximum number of rects", () => {
    const rects = Array.from({ length: PDF_LOCATOR_MAX_RECTS }, () => ({ x: 0, y: 0, w: 0.1, h: 0.1 }));
    expect(parsePdfLocator(buildPdfLocator({ page: 0, rects }))?.rects).toHaveLength(PDF_LOCATOR_MAX_RECTS);
  });
});

describe("pdfPageLabel", () => {
  it("shows the 1-based display page for a 0-based locator page", () => {
    expect(pdfPageLabel({ page: 0 })).toBe(1);
    expect(pdfPageLabel({ page: 41 })).toBe(42);
  });
});

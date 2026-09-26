import { describe, expect, it } from "vitest";
import { computeSelectionRects, mergeAdjacentRects } from "./pdf-selection";

function rect(x: number, y: number, w: number, h: number) {
  return { x, y, w, h };
}

function domRect(left: number, top: number, width: number, height: number): DOMRect {
  return { left, top, width, height, right: left + width, bottom: top + height, x: left, y: top, toJSON: () => ({}) };
}

/** A fake `Range` exposing only what `computeSelectionRects` reads. */
function fakeRange(clientRects: DOMRect[]): Range {
  return { getClientRects: () => clientRects } as unknown as Range;
}

describe("mergeAdjacentRects", () => {
  it("merges two rects on the same line with a small horizontal gap", () => {
    const merged = mergeAdjacentRects([rect(0.1, 0.2, 0.1, 0.02), rect(0.21, 0.2, 0.1, 0.02)]);
    expect(merged).toHaveLength(1);
    expect(merged[0].x).toBeCloseTo(0.1);
    expect(merged[0].y).toBeCloseTo(0.2);
    expect(merged[0].w).toBeCloseTo(0.21);
    expect(merged[0].h).toBeCloseTo(0.02);
  });

  it("keeps rects on different lines separate", () => {
    const merged = mergeAdjacentRects([rect(0.1, 0.2, 0.1, 0.02), rect(0.1, 0.4, 0.1, 0.02)]);
    expect(merged).toHaveLength(2);
  });

  it("keeps rects with a large horizontal gap on the same line separate (column wrap)", () => {
    const merged = mergeAdjacentRects([rect(0.05, 0.2, 0.1, 0.02), rect(0.6, 0.2, 0.1, 0.02)]);
    expect(merged).toHaveLength(2);
  });

  it("passes through a single rect unchanged", () => {
    expect(mergeAdjacentRects([rect(0.1, 0.1, 0.2, 0.02)])).toEqual([rect(0.1, 0.1, 0.2, 0.02)]);
  });

  it("returns an empty array for no rects", () => {
    expect(mergeAdjacentRects([])).toEqual([]);
  });
});

describe("computeSelectionRects", () => {
  it("normalizes client rects to the page's unscaled viewport", () => {
    const pageEl = { getBoundingClientRect: () => domRect(0, 0, 200, 400) } as unknown as HTMLElement;
    // scale 2: unscaled page is 100x200. A selection rect at (20,40) sized 40x10 (device px) is
    // (20/2)/100 = 0.1, (40/2)/200 = 0.1, (40/2)/100 = 0.2, (10/2)/200 = 0.025 in unscaled fractions.
    const range = fakeRange([domRect(20, 40, 40, 10)]);

    const rects = computeSelectionRects(range, pageEl, 2);

    expect(rects).toEqual([rect(0.1, 0.1, 0.2, 0.025)]);
  });

  it("skips zero-size client rects (empty text nodes)", () => {
    const pageEl = { getBoundingClientRect: () => domRect(0, 0, 100, 200) } as unknown as HTMLElement;
    const range = fakeRange([domRect(0, 0, 0, 0), domRect(10, 10, 10, 10)]);

    expect(computeSelectionRects(range, pageEl, 1)).toHaveLength(1);
  });

  it("returns no rects when the page has no measurable size", () => {
    const pageEl = { getBoundingClientRect: () => domRect(0, 0, 0, 0) } as unknown as HTMLElement;
    const range = fakeRange([domRect(0, 0, 10, 10)]);

    expect(computeSelectionRects(range, pageEl, 1)).toEqual([]);
  });
});

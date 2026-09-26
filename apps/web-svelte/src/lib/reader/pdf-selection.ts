import type { PdfRect } from "./pdf-locator";

/** Tolerance (in normalized 0-1 units) for treating two rects as being on the same text line. */
const LINE_MERGE_TOLERANCE = 0.006;
/** Gap (in normalized 0-1 units) beyond which two same-line rects are kept separate (line wraps, columns). */
const LINE_MERGE_MAX_GAP = 0.03;

/**
 * Converts a selection `Range`'s client rects into rects normalized to the page's unscaled viewport (0-1),
 * relative to the rendered page element and its current render scale. `pageEl` is the page's rendered wrapper
 * (holding the canvas at `scale`); its `getBoundingClientRect()` gives the on-screen page box, which divided by
 * `scale` yields the unscaled page size used to normalize.
 */
export function computeSelectionRects(range: Range, pageEl: HTMLElement, scale: number): PdfRect[] {
  const pageRect = pageEl.getBoundingClientRect();
  const pageWidth = pageRect.width / (scale || 1);
  const pageHeight = pageRect.height / (scale || 1);
  if (pageWidth <= 0 || pageHeight <= 0) return [];

  const rects: PdfRect[] = [];
  for (const clientRect of range.getClientRects()) {
    if (clientRect.width <= 0 || clientRect.height <= 0) continue;
    rects.push({
      x: (clientRect.left - pageRect.left) / (scale * pageWidth),
      y: (clientRect.top - pageRect.top) / (scale * pageHeight),
      w: clientRect.width / (scale * pageWidth),
      h: clientRect.height / (scale * pageHeight),
    });
  }
  return mergeAdjacentRects(rects);
}

/**
 * Merges rects that sit on the same text line (close vertical position and height) and are horizontally close
 * enough to be the same run of selected text, so a multi-span line of text becomes one highlight rect instead
 * of one per DOM text node. Distinct lines (or a line broken by a large horizontal gap, e.g. wrapped columns)
 * stay separate rects.
 */
export function mergeAdjacentRects(rects: readonly PdfRect[]): PdfRect[] {
  if (rects.length <= 1) return [...rects];

  const sorted = [...rects].sort((a, b) => a.y - b.y || a.x - b.x);
  const merged: PdfRect[] = [];
  for (const rect of sorted) {
    const last = merged[merged.length - 1];
    if (last && sameLine(last, rect) && horizontallyAdjacent(last, rect)) {
      const left = Math.min(last.x, rect.x);
      const right = Math.max(last.x + last.w, rect.x + rect.w);
      const top = Math.min(last.y, rect.y);
      const bottom = Math.max(last.y + last.h, rect.y + rect.h);
      merged[merged.length - 1] = { x: left, y: top, w: right - left, h: bottom - top };
    } else {
      merged.push({ ...rect });
    }
  }
  return merged;
}

function sameLine(a: PdfRect, b: PdfRect): boolean {
  return Math.abs(a.y - b.y) <= LINE_MERGE_TOLERANCE && Math.abs(a.h - b.h) <= LINE_MERGE_TOLERANCE * 2;
}

function horizontallyAdjacent(a: PdfRect, b: PdfRect): boolean {
  const gap = b.x >= a.x ? b.x - (a.x + a.w) : a.x - (b.x + b.w);
  return gap <= LINE_MERGE_MAX_GAP;
}

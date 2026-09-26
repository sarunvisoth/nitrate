/**
 * The `locator` payload for a `pdf`-kind Book annotation/bookmark: a 0-based page index plus the rects a
 * highlight covers (empty for a page bookmark), each normalized to the page's unscaled viewport (0-1 on both
 * axes, top-left origin) so the stored highlight survives zoom, rotation, and device size changes. Mirrors the
 * server's `PdfAnnotationLocator` (apps/backend/src/Prismedia.Application/Books/PdfAnnotationLocator.cs); PDF
 * locators never reuse EPUB CFI semantics (see VISION.md "PDF Reader").
 */
export interface PdfRect {
  x: number;
  y: number;
  w: number;
  h: number;
}

export interface PdfAnnotationLocator {
  page: number;
  rects: PdfRect[];
}

/** Highlights with more rects than this are rejected, mirroring the server's bound. */
export const PDF_LOCATOR_MAX_RECTS = 64;

/** Serializes a locator to its stored JSON string form. */
export function buildPdfLocator(locator: PdfAnnotationLocator): string {
  return JSON.stringify(locator);
}

/**
 * Parses and validates a PDF locator JSON string: `page` must be zero or greater, and there must be
 * 0-{@link PDF_LOCATOR_MAX_RECTS} rects, each coordinate within 0-1. Returns null for anything else (malformed
 * JSON, missing fields, out-of-range values).
 */
export function parsePdfLocator(json: string): PdfAnnotationLocator | null {
  let parsed: unknown;
  try {
    parsed = JSON.parse(json);
  } catch {
    return null;
  }

  if (!isRecord(parsed)) return null;
  const page = parsed.page;
  const rects = parsed.rects;
  if (typeof page !== "number" || !Number.isFinite(page) || page < 0) return null;
  if (!Array.isArray(rects) || rects.length > PDF_LOCATOR_MAX_RECTS) return null;

  const normalized: PdfRect[] = [];
  for (const rect of rects) {
    if (!isRecord(rect)) return null;
    const { x, y, w, h } = rect;
    if (![x, y, w, h].every(isUnitCoordinate)) return null;
    normalized.push({ x: x as number, y: y as number, w: w as number, h: h as number });
  }

  return { page, rects: normalized };
}

/** The 1-based page label shown to the reader, for a parsed (0-based) locator page. */
export function pdfPageLabel(locator: Pick<PdfAnnotationLocator, "page">): number {
  return locator.page + 1;
}

function isUnitCoordinate(value: unknown): boolean {
  return typeof value === "number" && Number.isFinite(value) && value >= 0 && value <= 1;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

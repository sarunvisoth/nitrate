// eslint-disable-next-line @typescript-eslint/no-explicit-any
type PdfDocument = any;
// eslint-disable-next-line @typescript-eslint/no-explicit-any
type PdfPage = any;
// eslint-disable-next-line @typescript-eslint/no-explicit-any
type PdfViewport = any;

/** One entry in a PDF's table of contents (pdf.js outline), normalized to page indices. */
export interface TocEntry {
  label: string;
  pageIndex: number | null;
  subitems: TocEntry[];
}

/** Resolves a pdf.js outline/link destination to a zero-based page index, or null if it can't. */
export async function resolveDestPage(pdf: PdfDocument, dest: unknown): Promise<number | null> {
  try {
    const explicit = typeof dest === "string" ? await pdf.getDestination(dest) : dest;
    if (Array.isArray(explicit) && explicit[0]) return await pdf.getPageIndex(explicit[0]);
  } catch {
    /* ignore */
  }
  return null;
}

/** Recursively normalizes a pdf.js outline into labeled entries with resolved page indices. */
export function normalizeOutline(
  items: unknown,
  resolve: (dest: unknown) => Promise<number | null>,
): Promise<TocEntry[]> {
  if (!Array.isArray(items)) return Promise.resolve([]);
  return Promise.all(
    items.map(async (item) => {
      const entry = item as { title?: unknown; dest?: unknown; items?: unknown };
      const label = typeof entry.title === "string" ? entry.title.trim() : "";
      const pageIndex = await resolve(entry.dest).catch(() => null);
      const subitems = await normalizeOutline(entry.items, resolve);
      return { label, pageIndex, subitems };
    }),
  ).then((entries) => entries.filter((e) => e.label.length > 0 || e.subitems.length > 0));
}

/**
 * Builds a transparent overlay of clickable links from the page's annotations: internal links
 * jump to the target page (via `onInternalLink`), external URLs open in a new tab. Positioned
 * via the viewport so it does not depend on pdf.js's annotation-layer CSS.
 */
export async function buildLinkLayer(
  page: PdfPage,
  viewport: PdfViewport,
  resolveDest: (dest: unknown) => Promise<number | null>,
  onInternalLink: (pageIndex: number) => void,
): Promise<HTMLDivElement | null> {
  let annotations: Array<Record<string, unknown>> = [];
  try {
    annotations = await page.getAnnotations();
  } catch {
    return null;
  }
  const links = annotations.filter(
    (a) => a.subtype === "Link" && (typeof a.url === "string" || a.dest != null),
  );
  if (links.length === 0) return null;

  const layer = document.createElement("div");
  layer.className = "pdf-link-layer";
  layer.style.width = `${viewport.width}px`;
  layer.style.height = `${viewport.height}px`;
  for (const annotation of links) {
    const rect = viewport.convertToViewportRectangle(annotation.rect as number[]);
    const left = Math.min(rect[0], rect[2]);
    const top = Math.min(rect[1], rect[3]);
    const width = Math.abs(rect[2] - rect[0]);
    const height = Math.abs(rect[3] - rect[1]);
    const link = document.createElement("a");
    link.className = "pdf-link";
    link.style.left = `${left}px`;
    link.style.top = `${top}px`;
    link.style.width = `${width}px`;
    link.style.height = `${height}px`;
    if (typeof annotation.url === "string") {
      link.href = annotation.url;
      link.target = "_blank";
      link.rel = "noopener noreferrer";
    } else {
      link.href = "#";
      const dest = annotation.dest;
      link.addEventListener("click", (event) => {
        event.preventDefault();
        void resolveDest(dest).then((idx) => {
          if (idx !== null) onInternalLink(idx);
        });
      });
    }
    layer.append(link);
  }
  return layer;
}

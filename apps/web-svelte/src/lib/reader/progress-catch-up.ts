import { exactWebEpubResumeLocation } from "$lib/entities/epub-contents";

/** The server's view of the user's progress on a book (from the entity's progress capability). */
export interface RemoteProgress {
  updatedAt: string | null | undefined;
  location: string | null | undefined;
  index: number | string | null | undefined;
  total: number | string | null | undefined;
}

/** What this device has already seen, saved, and is showing now. */
export interface LocalProgress {
  /**
   * The newest server `updatedAt` this device has already handled (seen at load or on a previous
   * check). Server clock only, so device clock drift cannot cause a jump.
   */
  lastSeenAt: string | null;
  /**
   * Positions this device saved itself recently (EPUB CFIs, or `page:N` keys for PDF). The server
   * echoing one of these back is our own save, never another device.
   */
  ownPositions?: ReadonlySet<string>;
  /** Current EPUB CFI on this device. */
  currentLocation: string | null;
  /** Current zero-based PDF page on this device. */
  currentPage?: number | null;
  pdf?: boolean;
}

/** Where to move the open reader so it matches progress saved on another device. */
export type ProgressTarget =
  | { kind: "cfi"; cfi: string }
  | { kind: "fraction"; fraction: number }
  | { kind: "page"; page: number };

/** Key used for PDF pages in {@link LocalProgress.ownPositions}. */
export function pdfPositionKey(page: number): string {
  return `page:${page}`;
}

/**
 * Decides whether an already-open reader should jump to progress another device saved.
 * Only progress newer than anything already seen, at a position this device did not save itself
 * and is not already showing, is adopted. EPUB prefers the exact CFI;
 * locators the web reader cannot use (the Apple apps' own formats) fall back to the saved fraction.
 */
export function remoteProgressToAdopt(remote: RemoteProgress, local: LocalProgress): ProgressTarget | null {
  const remoteAt = remote.updatedAt ? Date.parse(remote.updatedAt) : Number.NaN;
  if (!Number.isFinite(remoteAt)) return null;
  const seenAt = local.lastSeenAt ? Date.parse(local.lastSeenAt) : Number.NEGATIVE_INFINITY;
  if (!(remoteAt > seenAt)) return null;
  const own = local.ownPositions;

  const index = Number(remote.index ?? 0);
  const total = Number(remote.total ?? 0);
  if (local.pdf) {
    if (!Number.isFinite(index) || index < 0 || index === local.currentPage) return null;
    if (own?.has(pdfPositionKey(index))) return null;
    return { kind: "page", page: index };
  }

  const cfi = exactWebEpubResumeLocation(remote.location);
  if (cfi) return cfi === local.currentLocation || own?.has(cfi) ? null : { kind: "cfi", cfi };
  if (total > 0 && Number.isFinite(index)) {
    return { kind: "fraction", fraction: Math.max(0, Math.min(1, index / total)) };
  }
  return null;
}

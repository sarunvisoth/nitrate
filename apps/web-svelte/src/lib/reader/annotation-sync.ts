import type { BookAnnotation } from "$lib/api/generated/model";

/** Returns whichever version of the same annotation has the later `updatedAt` (ties keep `incoming`). */
export function mergeAnnotation(
  current: BookAnnotation | undefined,
  incoming: BookAnnotation,
): BookAnnotation {
  if (!current) return incoming;
  return Date.parse(incoming.updatedAt) >= Date.parse(current.updatedAt) ? incoming : current;
}

/** Folds a server list into the local map, keeping the newest version of each id. */
export function mergeAll(
  local: ReadonlyMap<string, BookAnnotation>,
  server: readonly BookAnnotation[],
): Map<string, BookAnnotation> {
  const next = new Map(local);
  for (const item of server) next.set(item.id, mergeAnnotation(next.get(item.id), item));
  return next;
}

/** Annotations that should be drawn (not tombstoned), in reading order. */
export function visibleAnnotations(items: Iterable<BookAnnotation>): BookAnnotation[] {
  return [...items]
    .filter((item) => !item.deletedAt)
    .sort(
      (a, b) =>
        Number(a.spineIndex) - Number(b.spineIndex) ||
        Number(a.position) - Number(b.position) ||
        Date.parse(a.createdAt) - Date.parse(b.createdAt),
    );
}

/** A new highlight captured in the reader, before the route assigns an id and clock. */
export interface AnnotationDraft {
  locator: string;
  spineIndex: number;
  position: number;
  selectedText: string;
  textBefore: string | null;
  textAfter: string | null;
  color: string;
  note: string | null;
}

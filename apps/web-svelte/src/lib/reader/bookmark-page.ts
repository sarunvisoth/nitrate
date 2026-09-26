import * as CFI from "$lib/vendor/foliate-js/epubcfi.js";

/** CFI of the first position on the visible page (the start of foliate's relocate range CFI). */
export function pageStartLocator(pageCfi: string | null | undefined): string | null {
  if (!pageCfi) return null;
  try {
    return CFI.collapse(pageCfi) as string;
  } catch {
    return null;
  }
}

/** The bookmark (if any) whose locator lies within the visible page's range CFI. */
export function bookmarkOnPage<T extends { locator: string }>(
  bookmarks: readonly T[],
  pageCfi: string | null | undefined,
): T | undefined {
  if (!pageCfi) return undefined;
  let start: string;
  let end: string;
  try {
    start = CFI.collapse(pageCfi) as string;
    end = CFI.collapse(pageCfi, true) as string;
  } catch {
    return undefined;
  }
  return bookmarks.find((bookmark) => {
    if (bookmark.locator === start) return true;
    try {
      return CFI.compare(bookmark.locator, start) >= 0 && CFI.compare(bookmark.locator, end) <= 0;
    } catch {
      return false;
    }
  });
}

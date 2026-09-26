import {
  deleteBookBookmark as deleteBookBookmarkRequest,
  listBookBookmarks,
  upsertBookBookmark,
} from "$lib/api/generated/prismedia";
import type { BookBookmark, BookBookmarksResponse } from "$lib/api/generated/model";
import { requestInit, unwrapGenerated, type RequestOptions } from "$lib/api/generated-response";

/** Fetches every bookmark the current user has on a Book, including deleted tombstones. */
export function fetchBookBookmarks(bookId: string, options?: RequestOptions): Promise<BookBookmarksResponse> {
  return listBookBookmarks(bookId, requestInit(options)).then((response) =>
    unwrapGenerated(response, `Failed to load bookmarks for book ${bookId}`),
  );
}

/** Creates or updates one bookmark; returns the stored (possibly newer) version. */
export function saveBookBookmark(
  bookId: string,
  bookmark: BookBookmark,
  options?: RequestOptions,
): Promise<BookBookmark> {
  return upsertBookBookmark(
    bookId,
    bookmark.id,
    {
      locatorKind: bookmark.locatorKind,
      locator: bookmark.locator,
      spineIndex: Number(bookmark.spineIndex),
      position: Number(bookmark.position),
      chapterLabel: bookmark.chapterLabel,
      label: bookmark.label,
      updatedAt: bookmark.updatedAt,
    },
    requestInit(options),
  ).then((response) => unwrapGenerated(response, `Failed to save bookmark ${bookmark.id}`));
}

/** Tombstones one bookmark using the caller's clock; returns the stored (possibly newer) version. */
export function deleteBookBookmark(
  bookId: string,
  bookmarkId: string,
  updatedAt: string,
  options?: RequestOptions,
): Promise<BookBookmark> {
  return deleteBookBookmarkRequest(bookId, bookmarkId, { updatedAt }, requestInit(options)).then((response) =>
    unwrapGenerated(response, `Failed to delete bookmark ${bookmarkId}`),
  );
}

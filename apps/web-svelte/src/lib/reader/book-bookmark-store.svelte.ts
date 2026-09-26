import type { BookBookmark, BookBookmarksResponse } from "$lib/api/generated/model";
import { ANNOTATION_LOCATOR_KIND } from "$lib/api/generated/codes";
import { deleteBookBookmark, fetchBookBookmarks, saveBookBookmark } from "$lib/api/bookmarks";
import { createUuid } from "$lib/utils/uuid";
import { SyncedItemStore } from "./synced-item-store.svelte";

/** Server operations the store needs; injectable for tests. */
export interface BookBookmarkApi {
  list(bookId: string): Promise<BookBookmarksResponse>;
  save(bookId: string, bookmark: BookBookmark): Promise<BookBookmark>;
  remove(bookId: string, bookmarkId: string, updatedAt: string): Promise<BookBookmark>;
}

/** Default API backed by the generated client. */
export const bookBookmarkApi: BookBookmarkApi = {
  list: (bookId) => fetchBookBookmarks(bookId),
  save: (bookId, bookmark) => saveBookBookmark(bookId, bookmark),
  remove: (bookId, bookmarkId, updatedAt) => deleteBookBookmark(bookId, bookmarkId, updatedAt),
};

/** The page a new bookmark points at. */
export interface BookmarkPage {
  locator: string;
  spineIndex: number;
  position: number;
  chapterLabel: string | null;
}

/** One Book's bookmarks, synced across devices (see {@link SyncedItemStore}). */
export class BookBookmarkStore extends SyncedItemStore<BookBookmark> {
  constructor(
    private readonly bookId: string,
    api: BookBookmarkApi = bookBookmarkApi,
    now: () => string = () => new Date().toISOString(),
    private readonly newId: () => string = createUuid,
  ) {
    super(
      {
        list: async () => (await api.list(bookId)).bookmarks,
        save: (bookmark) => api.save(bookId, bookmark),
        remove: (id, updatedAt) => api.remove(bookId, id, updatedAt),
      },
      now,
    );
  }

  /** Non-deleted bookmarks in reading order. */
  get visible(): BookBookmark[] {
    return this.all
      .filter((item) => !item.deletedAt)
      .sort((a, b) => Number(a.spineIndex) - Number(b.spineIndex) || Number(a.position) - Number(b.position));
  }

  /** Bookmarks a page and returns the new bookmark immediately. */
  add(page: BookmarkPage, locatorKind: string = ANNOTATION_LOCATOR_KIND.epubCfi): BookBookmark {
    const at = this.now();
    const bookmark: BookBookmark = {
      id: this.newId(),
      bookId: this.bookId,
      locatorKind,
      ...page,
      label: null,
      createdAt: at,
      updatedAt: at,
      deletedAt: null,
    };
    this.write(bookmark);
    return bookmark;
  }

  /** Sets or clears a bookmark's label. */
  rename(bookmark: BookBookmark, label: string): void {
    const trimmed = label.trim();
    this.write({ ...bookmark, label: trimmed || null, updatedAt: this.now(), deletedAt: null });
  }

  /** Deletes (tombstones) a bookmark. */
  remove(bookmark: BookBookmark): void {
    const at = this.now();
    this.write({ ...bookmark, updatedAt: at, deletedAt: at });
  }
}

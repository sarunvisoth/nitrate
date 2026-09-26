import type { BookAnnotation, BookAnnotationsResponse } from "$lib/api/generated/model";
import { ANNOTATION_LOCATOR_KIND, ANNOTATION_STYLE } from "$lib/api/generated/codes";
import { deleteBookAnnotation, fetchBookAnnotations, saveBookAnnotation } from "$lib/api/annotations";
import { createUuid } from "$lib/utils/uuid";
import { visibleAnnotations, type AnnotationDraft } from "./annotation-sync";
import { SyncedItemStore } from "./synced-item-store.svelte";

/** Server operations the store needs; injectable for tests. */
export interface BookAnnotationApi {
  list(bookId: string): Promise<BookAnnotationsResponse>;
  save(bookId: string, annotation: BookAnnotation): Promise<BookAnnotation>;
  remove(bookId: string, annotationId: string, updatedAt: string): Promise<BookAnnotation>;
}

/** Default API backed by the generated client. */
export const bookAnnotationApi: BookAnnotationApi = {
  list: (bookId) => fetchBookAnnotations(bookId),
  save: (bookId, annotation) => saveBookAnnotation(bookId, annotation),
  remove: (bookId, annotationId, updatedAt) => deleteBookAnnotation(bookId, annotationId, updatedAt),
};

/** One Book's highlights, synced across devices (see {@link SyncedItemStore}). */
export class BookAnnotationStore extends SyncedItemStore<BookAnnotation> {
  constructor(
    private readonly bookId: string,
    api: BookAnnotationApi = bookAnnotationApi,
    now: () => string = () => new Date().toISOString(),
    private readonly newId: () => string = createUuid,
  ) {
    super(
      {
        list: async () => (await api.list(bookId)).annotations,
        save: (annotation) => api.save(bookId, annotation),
        remove: (id, updatedAt) => api.remove(bookId, id, updatedAt),
      },
      now,
    );
  }

  /** Non-deleted annotations in reading order. */
  get visible(): BookAnnotation[] {
    return visibleAnnotations(this.all);
  }

  /** Creates a highlight from a reader selection and returns it immediately. */
  create(draft: AnnotationDraft, locatorKind: string = ANNOTATION_LOCATOR_KIND.epubCfi): BookAnnotation {
    const at = this.now();
    const annotation: BookAnnotation = {
      id: this.newId(),
      bookId: this.bookId,
      locatorKind,
      style: ANNOTATION_STYLE.highlight,
      ...draft,
      sourceSignature: null,
      createdAt: at,
      updatedAt: at,
      deletedAt: null,
    };
    this.write(annotation);
    return annotation;
  }

  /** Saves an edited colour, note, or repaired locator. */
  update(annotation: BookAnnotation): void {
    this.write({ ...annotation, updatedAt: this.now(), deletedAt: null });
  }

  /** Deletes (tombstones) a highlight. */
  remove(annotation: BookAnnotation): void {
    const at = this.now();
    this.write({ ...annotation, updatedAt: at, deletedAt: at });
  }
}

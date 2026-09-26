import type { BookAnnotation, BookAnnotationsResponse } from "$lib/api/generated/model";
import { ANNOTATION_LOCATOR_KIND, ANNOTATION_STYLE } from "$lib/api/generated/codes";
import { deleteBookAnnotation, fetchBookAnnotations, saveBookAnnotation } from "$lib/api/annotations";
import { createUuid } from "$lib/utils/uuid";
import { mergeAll, visibleAnnotations, type AnnotationDraft } from "./annotation-sync";

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

/**
 * Reader-side copy of one Book's annotations. Writes apply locally first (optimistic), are sent to
 * the server one at a time, and the server's answer is merged back (last write wins on
 * `updatedAt`, so a newer edit from another device replaces ours). Writes that fail stay pending
 * and are resent on the next `refresh()` (tab focus / visibility), in memory only.
 */
export class BookAnnotationStore {
  #items = $state.raw(new Map<string, BookAnnotation>());
  readonly #pending = new Map<string, BookAnnotation>();
  #queue: Promise<void> = Promise.resolve();

  constructor(
    private readonly bookId: string,
    private readonly api: BookAnnotationApi = bookAnnotationApi,
    private readonly now: () => string = () => new Date().toISOString(),
    private readonly newId: () => string = createUuid,
  ) {}

  /** Non-deleted annotations in reading order. */
  get visible(): BookAnnotation[] {
    return visibleAnnotations(this.#items.values());
  }

  /** Pulls the server list, merges it, and resends local writes the server has not seen. */
  async refresh(): Promise<void> {
    const response = await this.api.list(this.bookId);
    this.#items = mergeAll(this.#items, response.annotations);
    for (const local of this.#pending.values()) {
      const server = response.annotations.find((item) => item.id === local.id);
      if (!server || Date.parse(local.updatedAt) > Date.parse(server.updatedAt)) this.#send(local);
      else this.#pending.delete(local.id);
    }
  }

  /** Creates a highlight from a reader selection and returns it immediately. */
  create(draft: AnnotationDraft): BookAnnotation {
    const at = this.now();
    const annotation: BookAnnotation = {
      id: this.newId(),
      bookId: this.bookId,
      locatorKind: ANNOTATION_LOCATOR_KIND.epubCfi,
      style: ANNOTATION_STYLE.highlight,
      ...draft,
      sourceSignature: null,
      createdAt: at,
      updatedAt: at,
      deletedAt: null,
    };
    this.#write(annotation);
    return annotation;
  }

  /** Saves an edited colour or note. */
  update(annotation: BookAnnotation): void {
    this.#write({ ...annotation, updatedAt: this.now(), deletedAt: null });
  }

  /** Deletes (tombstones) a highlight. */
  remove(annotation: BookAnnotation): void {
    const at = this.now();
    this.#write({ ...annotation, updatedAt: at, deletedAt: at });
  }

  /** Resolves once every queued write has been attempted. */
  idle(): Promise<void> {
    return this.#queue;
  }

  #write(annotation: BookAnnotation) {
    this.#items = new Map(this.#items).set(annotation.id, annotation);
    this.#pending.set(annotation.id, annotation);
    this.#send(annotation);
  }

  #send(annotation: BookAnnotation) {
    this.#queue = this.#queue.then(async () => {
      try {
        const stored = annotation.deletedAt
          ? await this.api.remove(this.bookId, annotation.id, annotation.updatedAt)
          : await this.api.save(this.bookId, annotation);
        this.#items = mergeAll(this.#items, [stored]);
        if (this.#pending.get(annotation.id) === annotation) this.#pending.delete(annotation.id);
      } catch {
        // Keep it pending; the next refresh() resends it.
      }
    });
  }
}

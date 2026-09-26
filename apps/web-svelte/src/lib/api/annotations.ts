import {
  deleteBookAnnotation as deleteBookAnnotationRequest,
  listBookAnnotations,
  upsertBookAnnotation,
} from "$lib/api/generated/prismedia";
import type { BookAnnotation, BookAnnotationsResponse } from "$lib/api/generated/model";
import { requestInit, unwrapGenerated, type RequestOptions } from "$lib/api/generated-response";

/** Fetches every annotation the current user has on a Book, including deleted tombstones. */
export function fetchBookAnnotations(
  bookId: string,
  options?: RequestOptions,
): Promise<BookAnnotationsResponse> {
  return listBookAnnotations(bookId, requestInit(options)).then((response) =>
    unwrapGenerated(response, `Failed to load annotations for book ${bookId}`),
  );
}

/**
 * Creates or updates one annotation. The server keeps whichever write has the newest `updatedAt`
 * and returns the stored version, which may be a newer one from another device.
 */
export function saveBookAnnotation(
  bookId: string,
  annotation: BookAnnotation,
  options?: RequestOptions,
): Promise<BookAnnotation> {
  return upsertBookAnnotation(
    bookId,
    annotation.id,
    {
      locatorKind: annotation.locatorKind,
      locator: annotation.locator,
      spineIndex: Number(annotation.spineIndex),
      position: Number(annotation.position),
      selectedText: annotation.selectedText,
      textBefore: annotation.textBefore,
      textAfter: annotation.textAfter,
      style: annotation.style,
      color: annotation.color,
      note: annotation.note,
      updatedAt: annotation.updatedAt,
    },
    requestInit(options),
  ).then((response) => unwrapGenerated(response, `Failed to save annotation ${annotation.id}`));
}

/** Tombstones one annotation using the caller's clock; returns the stored (possibly newer) version. */
export function deleteBookAnnotation(
  bookId: string,
  annotationId: string,
  updatedAt: string,
  options?: RequestOptions,
): Promise<BookAnnotation> {
  return deleteBookAnnotationRequest(bookId, annotationId, { updatedAt }, requestInit(options)).then(
    (response) => unwrapGenerated(response, `Failed to delete annotation ${annotationId}`),
  );
}

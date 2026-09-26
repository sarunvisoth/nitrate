import {
  getExportAnnotationLibraryUrl,
  listAnnotationLibrary,
  listAnnotationLibraryBooks,
} from "$lib/api/generated/prismedia";
import type { AnnotationLibraryBooksResponse, AnnotationLibraryResponse } from "$lib/api/generated/model";
import type { AnnotationExportFormatCode } from "$lib/api/generated/codes";
import { apiPath } from "$lib/api/orval-fetch";
import { requestInit, unwrapGenerated, type RequestOptions } from "$lib/api/generated-response";

/** Dashboard filters; unset fields are not sent. */
export interface AnnotationLibraryFilters {
  search?: string;
  bookId?: string;
  color?: string;
  notesOnly?: boolean;
  cursor?: string;
  limit?: number;
}

/** Searches the current user's highlights and notes across every visible book. */
export function fetchAnnotationLibrary(
  filters: AnnotationLibraryFilters,
  options?: RequestOptions,
): Promise<AnnotationLibraryResponse> {
  const params = {
    ...(filters.search?.trim() ? { search: filters.search.trim() } : {}),
    ...(filters.bookId ? { bookId: filters.bookId } : {}),
    ...(filters.color ? { color: filters.color } : {}),
    ...(filters.notesOnly ? { notesOnly: true } : {}),
    ...(filters.cursor ? { cursor: filters.cursor } : {}),
    ...(filters.limit ? { limit: filters.limit } : {}),
  };
  return listAnnotationLibrary(Object.keys(params).length ? params : undefined, requestInit(options)).then(
    (response) => unwrapGenerated(response, "Failed to load annotations"),
  );
}

/** Every book the current user has highlighted, with counts. */
export function fetchAnnotationLibraryBooks(options?: RequestOptions): Promise<AnnotationLibraryBooksResponse> {
  return listAnnotationLibraryBooks(requestInit(options)).then((response) =>
    unwrapGenerated(response, "Failed to load annotated books"),
  );
}

/** Same-origin download link for an export (the session cookie authenticates a plain link). */
export function annotationExportUrl(format: AnnotationExportFormatCode, bookId?: string): string {
  return apiPath(getExportAnnotationLibraryUrl({ format, ...(bookId ? { bookId } : {}) }));
}

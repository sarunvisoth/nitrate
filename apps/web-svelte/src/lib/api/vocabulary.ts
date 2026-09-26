import {
  deleteVocabularyEntry as deleteVocabularyEntryRequest,
  getExportVocabularyUrl,
  listVocabulary,
  putVocabularyEntry,
} from "$lib/api/generated/prismedia";
import type { UpsertVocabularyEntryRequest, VocabularyEntry, VocabularyListResponse } from "$lib/api/generated/model";
import type { AnnotationExportFormatCode } from "$lib/api/generated/codes";
import { apiPath } from "$lib/api/orval-fetch";
import { requestInit, unwrapGenerated, type RequestOptions } from "$lib/api/generated-response";

/** Filters for one page of the current user's vocabulary list. */
export interface VocabularyFilters {
  search?: string;
  status?: string;
  bookId?: string;
  cursor?: string;
  limit?: number;
}

/** Fetches one page of the current user's saved vocabulary. */
export function fetchVocabulary(
  filters: VocabularyFilters,
  options?: RequestOptions,
): Promise<VocabularyListResponse> {
  const params = {
    ...(filters.search?.trim() ? { search: filters.search.trim() } : {}),
    ...(filters.status ? { status: filters.status } : {}),
    ...(filters.bookId ? { bookId: filters.bookId } : {}),
    ...(filters.cursor ? { cursor: filters.cursor } : {}),
    ...(filters.limit ? { limit: filters.limit } : {}),
  };
  return listVocabulary(Object.keys(params).length ? params : undefined, requestInit(options)).then((response) =>
    unwrapGenerated(response, "Failed to load vocabulary"),
  );
}

/**
 * Creates or updates one saved word; when the word/language pair is already saved on another
 * entry, the server updates that entry instead and returns it (its id may differ from `entryId`).
 */
export function saveVocabularyEntry(
  entryId: string,
  request: UpsertVocabularyEntryRequest,
  options?: RequestOptions,
): Promise<VocabularyEntry> {
  return putVocabularyEntry(entryId, request, requestInit(options)).then((response) =>
    unwrapGenerated(response, "Failed to save vocabulary entry"),
  );
}

/** Tombstones one saved word using the caller's clock. */
export function deleteVocabularyEntry(
  entryId: string,
  updatedAt: string,
  options?: RequestOptions,
): Promise<VocabularyEntry> {
  return deleteVocabularyEntryRequest(entryId, { updatedAt }, requestInit(options)).then((response) =>
    unwrapGenerated(response, "Failed to delete vocabulary entry"),
  );
}

/** Same-origin download link for a vocabulary export (the session cookie authenticates a plain link). */
export function vocabularyExportUrl(format: AnnotationExportFormatCode, bookId?: string): string {
  return apiPath(getExportVocabularyUrl({ format, ...(bookId ? { bookId } : {}) }));
}

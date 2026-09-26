import { getReaderPreferences, putReaderPreferences } from "$lib/api/generated/prismedia";
import type { ReaderPreferencesResponse, UpsertReaderPreferencesRequest } from "$lib/api/generated/model";
import { requestInit, unwrapGenerated, type RequestOptions } from "$lib/api/generated-response";

/** Fetches the current user's reader preferences, or the server's documented defaults. */
export function fetchReaderPreferences(options?: RequestOptions): Promise<ReaderPreferencesResponse> {
  return getReaderPreferences(requestInit(options)).then((response) =>
    unwrapGenerated(response, "Failed to load reader preferences"),
  );
}

/** Creates or updates the current user's reader preferences; returns the stored (possibly newer) version. */
export function saveReaderPreferences(
  request: UpsertReaderPreferencesRequest,
  options?: RequestOptions,
): Promise<ReaderPreferencesResponse> {
  return putReaderPreferences(request, requestInit(options)).then((response) =>
    unwrapGenerated(response, "Failed to save reader preferences"),
  );
}

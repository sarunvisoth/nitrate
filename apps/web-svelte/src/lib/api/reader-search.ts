import { searchReader } from "$lib/api/generated/prismedia";
import type { ReaderSearchResponse } from "$lib/api/generated/model";
import { requestInit, unwrapGenerated, type RequestOptions } from "$lib/api/generated-response";

/** Fetches the current user's matching highlights/notes and saved vocabulary words. */
export function fetchReaderSearch(
  query: string,
  limit: number,
  options?: RequestOptions,
): Promise<ReaderSearchResponse> {
  return searchReader({ q: query, limit }, requestInit(options)).then((response) =>
    unwrapGenerated(response, "Failed to search annotations and vocabulary"),
  );
}

import {
  defineReaderTerm,
  getReaderWikipediaSummary,
  translateReaderText,
} from "$lib/api/generated/prismedia";
import type {
  DictionaryDefinitionResponse,
  TranslateTextResponse,
  WikipediaSummaryResponse,
} from "$lib/api/generated/model";
import { problemMessage, requestInit, type RequestOptions } from "$lib/api/generated-response";

/**
 * Outcome of one reader-lookup call. Modeled as a discriminated union (rather than throwing) so
 * the lookup popover can show a clean "no result" state separately from "the provider is down".
 */
export type ReaderLookupOutcome<T> =
  | { status: "ok"; data: T }
  | { status: "not-found" }
  | { status: "invalid"; message: string }
  | { status: "unavailable"; message: string };

function toOutcome<T>(response: { data: unknown; status: number }): ReaderLookupOutcome<T> {
  if (response.status === 200) return { status: "ok", data: response.data as T };
  if (response.status === 404) return { status: "not-found" };
  if (response.status === 400) {
    return { status: "invalid", message: problemMessage(response.data) ?? "The request is invalid." };
  }
  return {
    status: "unavailable",
    message: problemMessage(response.data) ?? "This lookup service is temporarily unavailable.",
  };
}

/** Looks up a dictionary definition for a single word. */
export function defineTerm(
  term: string,
  language: string,
  options?: RequestOptions,
): Promise<ReaderLookupOutcome<DictionaryDefinitionResponse>> {
  return defineReaderTerm({ term, language }, requestInit(options)).then(
    toOutcome<DictionaryDefinitionResponse>,
  );
}

/** Translates a passage of text (at most 500 characters). */
export function translateText(
  text: string,
  targetLanguage: string,
  sourceLanguage: string | undefined,
  options?: RequestOptions,
): Promise<ReaderLookupOutcome<TranslateTextResponse>> {
  return translateReaderText(
    { text, sourceLanguage: sourceLanguage ?? null, targetLanguage },
    requestInit(options),
  ).then(toOutcome<TranslateTextResponse>);
}

/** Looks up a Wikipedia summary for a term. */
export function wikipediaSummary(
  term: string,
  language: string,
  options?: RequestOptions,
): Promise<ReaderLookupOutcome<WikipediaSummaryResponse>> {
  return getReaderWikipediaSummary({ term, language }, requestInit(options)).then(
    toOutcome<WikipediaSummaryResponse>,
  );
}

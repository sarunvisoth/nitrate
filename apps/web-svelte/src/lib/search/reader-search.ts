import { fetchReaderSearch } from "$lib/api/reader-search";
import { bookReaderHref } from "$lib/entities/book-reader-route";
import { READER_SEARCH_GROUP_KIND, type ReaderSearchGroupKindCode } from "$lib/api/generated/codes";
import type { ReaderSearchResponse } from "$lib/api/generated/model";

export type ReaderSearchFetcher = (query: string, limit: number) => Promise<ReaderSearchResponse>;

/** One matching highlight/note or saved vocabulary word, ready to render in a result list. */
export interface ReaderSearchResultItem {
  id: string;
  kind: ReaderSearchGroupKindCode;
  /** Book title for an annotation; the saved word for a vocabulary entry. */
  title: string;
  /** Quoted highlight text, or the definition. */
  snippet: string;
  /** Annotation colour code, or vocabulary status code. */
  meta: string | null;
  href: string;
}

/** One group of reader-domain search results (annotations or vocabulary), with a "see all" link. */
export interface ReaderSearchGroup {
  kind: ReaderSearchGroupKindCode;
  label: string;
  items: ReaderSearchResultItem[];
  seeAllHref: string;
}

const MAX_SNIPPET_LENGTH = 160;

/**
 * Searches the current user's highlights/notes and saved vocabulary words. Returns only the
 * groups that have at least one match, each capped at `limit` items.
 */
export async function searchReaderExtras(
  query: string,
  limit: number,
  fetcher: ReaderSearchFetcher = fetchReaderSearch,
): Promise<ReaderSearchGroup[]> {
  const trimmed = query.trim();
  if (trimmed.length < 2) return [];

  const response = await fetcher(trimmed, limit);
  const encodedQuery = encodeURIComponent(trimmed);

  const annotationGroup: ReaderSearchGroup = {
    kind: READER_SEARCH_GROUP_KIND.annotation,
    label: "Annotations",
    seeAllHref: `/annotations?q=${encodedQuery}`,
    items: response.annotations.map((item) => ({
      id: item.id,
      kind: READER_SEARCH_GROUP_KIND.annotation,
      title: item.bookTitle,
      snippet: truncate(item.selectedText),
      meta: item.color,
      href: bookReaderHref({ bookId: item.bookId, kind: "book", id: item.bookId, location: item.locator }),
    })),
  };

  const vocabularyGroup: ReaderSearchGroup = {
    kind: READER_SEARCH_GROUP_KIND.vocabulary,
    label: "Vocabulary",
    seeAllHref: `/vocabulary?q=${encodedQuery}`,
    items: response.vocabulary.map((item) => ({
      id: item.id,
      kind: READER_SEARCH_GROUP_KIND.vocabulary,
      title: item.word,
      snippet: truncate(item.definition ?? item.sourceSentence ?? ""),
      meta: item.status,
      href:
        item.bookId && item.sourceLocator
          ? bookReaderHref({ bookId: item.bookId, kind: "book", id: item.bookId, location: item.sourceLocator })
          : `/vocabulary?q=${encodeURIComponent(item.word)}`,
    })),
  };

  return [annotationGroup, vocabularyGroup].filter((group) => group.items.length > 0);
}

function truncate(text: string): string {
  const trimmed = text.trim();
  return trimmed.length > MAX_SNIPPET_LENGTH ? `${trimmed.slice(0, MAX_SNIPPET_LENGTH - 1)}…` : trimmed;
}

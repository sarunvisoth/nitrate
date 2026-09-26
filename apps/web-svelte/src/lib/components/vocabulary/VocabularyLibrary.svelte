<script lang="ts" module>
  import type { VocabularyListResponse } from "$lib/api/generated/model";
  import type { VocabularyFilters } from "$lib/api/vocabulary";

  /** Data access for the dashboard; injectable for tests. */
  export interface VocabularyLibraryLoaders {
    list: (filters: VocabularyFilters) => Promise<VocabularyListResponse>;
  }
</script>

<script lang="ts">
  import { onMount } from "svelte";
  import { Badge, Button, Empty, SearchInput, Select, buttonVariants } from "@prismedia/ui-svelte";
  import { Download, Trash2, BookMarked } from "@lucide/svelte";
  import type { VocabularyEntry } from "$lib/api/generated/model";
  import { ANNOTATION_EXPORT_FORMAT, VOCABULARY_STATUS, type VocabularyStatusCode } from "$lib/api/generated/codes";
  import { deleteVocabularyEntry, fetchVocabulary, saveVocabularyEntry, vocabularyExportUrl } from "$lib/api/vocabulary";
  import { bookReaderHref } from "$lib/entities/book-reader-route";

  let {
    loaders = { list: fetchVocabulary },
    initialSearch = "",
  }: { loaders?: VocabularyLibraryLoaders; initialSearch?: string } = $props();

  const ALL = "";
  const PAGE_SIZE = 200;
  const SEARCH_DEBOUNCE_MS = 250;
  const STATUS_LABEL: Record<VocabularyStatusCode, string> = {
    new: "New",
    learning: "Learning",
    familiar: "Familiar",
    known: "Known",
  };
  const STATUS_OPTIONS = [
    { value: ALL, label: "All statuses" },
    ...Object.values(VOCABULARY_STATUS).map((code) => ({ value: code, label: STATUS_LABEL[code] })),
  ];

  let search = $state(initialSearch);
  let statusFilter = $state(ALL);
  let items = $state.raw<VocabularyEntry[]>([]);
  let totalCount = $state(0);
  let nextCursor = $state<string | null>(null);
  let loading = $state(true);
  let error = $state<string | null>(null);
  let requestSeq = 0;
  let searchTimer: ReturnType<typeof setTimeout> | null = null;

  const hasFilters = $derived(Boolean(search.trim() || statusFilter));

  async function load(append = false) {
    const seq = ++requestSeq;
    loading = true;
    error = null;
    try {
      const page = await loaders.list({
        search,
        status: statusFilter || undefined,
        cursor: append ? (nextCursor ?? undefined) : undefined,
        limit: PAGE_SIZE,
      });
      if (seq !== requestSeq) return;
      items = append ? [...items, ...page.items] : page.items;
      totalCount = Number(page.totalCount);
      nextCursor = page.nextCursor ?? null;
    } catch (err) {
      if (seq === requestSeq) error = err instanceof Error ? err.message : String(err);
    } finally {
      if (seq === requestSeq) loading = false;
    }
  }

  function reload() {
    void load(false);
  }

  function onSearchInput() {
    if (searchTimer) clearTimeout(searchTimer);
    searchTimer = setTimeout(reload, SEARCH_DEBOUNCE_MS);
  }

  async function changeStatus(entry: VocabularyEntry, status: VocabularyStatusCode) {
    const updated = await saveVocabularyEntry(entry.id, {
      word: entry.word,
      language: entry.language,
      definition: entry.definition,
      bookId: entry.bookId,
      sourceLocator: entry.sourceLocator,
      sourceSentence: entry.sourceSentence,
      status,
      updatedAt: new Date().toISOString(),
    });
    items = items.map((item) => (item.id === entry.id ? updated : item));
  }

  async function remove(entry: VocabularyEntry) {
    await deleteVocabularyEntry(entry.id, new Date().toISOString());
    items = items.filter((item) => item.id !== entry.id);
    totalCount = Math.max(0, totalCount - 1);
  }

  function sourceHref(entry: VocabularyEntry) {
    return entry.bookId && entry.sourceLocator
      ? bookReaderHref({ bookId: entry.bookId, kind: "book", id: entry.bookId, location: entry.sourceLocator })
      : null;
  }

  onMount(() => {
    reload();
    return () => {
      if (searchTimer) clearTimeout(searchTimer);
    };
  });
</script>

<section class="space-y-4 pb-6">
  <header class="flex flex-col gap-3 border-l-2 border-[var(--color-material-spectrum-cyan)] pl-3 lg:flex-row lg:items-end lg:justify-between">
    <div class="min-w-0">
      <h1 class="flex items-center gap-2 font-heading text-xl font-semibold tracking-tight text-text-primary">
        <BookMarked class="h-5 w-5 text-text-muted" aria-hidden="true" />
        Vocabulary
      </h1>
      <p class="mt-1 max-w-2xl text-sm leading-relaxed text-text-muted">
        {totalCount} saved {totalCount === 1 ? "word" : "words"} from Define while reading.
      </p>
    </div>

    <div class="flex flex-wrap items-center gap-2" role="group" aria-label="Export vocabulary">
      <Download class="h-4 w-4 text-text-muted" aria-hidden="true" />
      <a class={buttonVariants({ variant: "outline", size: "sm" })} href={vocabularyExportUrl(ANNOTATION_EXPORT_FORMAT.markdown)} download>Markdown</a>
      <a class={buttonVariants({ variant: "outline", size: "sm" })} href={vocabularyExportUrl(ANNOTATION_EXPORT_FORMAT.json)} download>JSON</a>
      <a class={buttonVariants({ variant: "outline", size: "sm" })} href={vocabularyExportUrl(ANNOTATION_EXPORT_FORMAT.csv)} download>CSV</a>
    </div>
  </header>

  <div class="flex flex-col gap-2 sm:flex-row sm:flex-wrap sm:items-center">
    <SearchInput
      class="sm:w-80"
      ariaLabel="Search vocabulary"
      placeholder="Search words and definitions"
      bind:value={search}
      oninput={onSearchInput}
      onClear={reload}
    />
    <Select size="sm" value={statusFilter} options={STATUS_OPTIONS} ariaLabel="Filter by status"
      onchange={(value) => { statusFilter = value; reload(); }} />
  </div>

  {#if error}
    <div class="surface-panel border-l-2 border-error px-3 py-2 text-sm text-error-text" role="alert">{error}</div>
  {:else if !loading && items.length === 0}
    <Empty.Root>
      <Empty.Header>
        <Empty.Media variant="icon"><BookMarked /></Empty.Media>
        <Empty.Title>{hasFilters ? "No matching words" : "No vocabulary yet"}</Empty.Title>
        <Empty.Description>
          {hasFilters
            ? "Try a different search or clear the filters."
            : "Select a word while reading an EPUB, choose Define, then Add to Vocabulary."}
        </Empty.Description>
      </Empty.Header>
    </Empty.Root>
  {:else}
    <ul class="flex flex-col divide-y divide-border-subtle surface-panel">
      {#each items as entry (entry.id)}
        <li class="flex flex-wrap items-start gap-3 px-3 py-3">
          <div class="min-w-0 flex-1 space-y-1">
            <p class="flex flex-wrap items-baseline gap-2">
              {#if sourceHref(entry)}
                <a href={sourceHref(entry)} class="text-sm font-semibold text-text-primary hover:underline">{entry.word}</a>
              {:else}
                <span class="text-sm font-semibold text-text-primary">{entry.word}</span>
              {/if}
              <span class="font-mono text-[0.6rem] uppercase tracking-[0.14em] text-text-muted">{entry.language}</span>
              {#if entry.bookTitle}
                <Badge variant="outline">{entry.bookTitle}</Badge>
              {/if}
            </p>
            {#if entry.definition}
              <p class="text-sm text-text-secondary">{entry.definition}</p>
            {/if}
            {#if entry.sourceSentence}
              <p class="text-sm italic text-text-muted">“{entry.sourceSentence}”</p>
            {/if}
          </div>
          <div class="flex items-center gap-2">
            <Select size="sm" value={entry.status} ariaLabel={`Status for ${entry.word}`}
              options={Object.values(VOCABULARY_STATUS).map((code) => ({ value: code, label: STATUS_LABEL[code] }))}
              onchange={(value) => changeStatus(entry, value as VocabularyStatusCode)} />
            <Button variant="ghost" size="icon" type="button" aria-label={`Delete ${entry.word}`} onclick={() => remove(entry)}>
              <Trash2 class="h-4 w-4" />
            </Button>
          </div>
        </li>
      {/each}
    </ul>
    {#if nextCursor}
      <Button variant="outline" size="sm" type="button" disabled={loading} onclick={() => load(true)}>Load more</Button>
    {/if}
  {/if}
</section>

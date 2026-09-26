<script lang="ts" module>
  import type { AnnotationLibraryBooksResponse, AnnotationLibraryResponse } from "$lib/api/generated/model";
  import type { AnnotationLibraryFilters } from "$lib/api/annotation-library";

  /** Data access for the dashboard; injectable for tests. */
  export interface AnnotationLibraryLoaders {
    list: (filters: AnnotationLibraryFilters) => Promise<AnnotationLibraryResponse>;
    books: () => Promise<AnnotationLibraryBooksResponse>;
  }
</script>

<script lang="ts">
  import { onMount } from "svelte";
  import { Badge, Button, Empty, SearchInput, Select, Toggle, buttonVariants } from "@prismedia/ui-svelte";
  import { Download, Highlighter, NotebookPen } from "@lucide/svelte";
  import type { AnnotationLibraryBook, AnnotationLibraryItem } from "$lib/api/generated/model";
  import { ANNOTATION_EXPORT_FORMAT } from "$lib/api/generated/codes";
  import {
    annotationExportUrl,
    fetchAnnotationLibrary,
    fetchAnnotationLibraryBooks,
  } from "$lib/api/annotation-library";
  import {
    ANNOTATION_COLOR_LABEL,
    ANNOTATION_COLOR_ORDER,
    annotationColorPaint,
  } from "$lib/reader/annotation-colors";
  import { bookReaderHref } from "$lib/entities/book-reader-route";
  import { parsePdfLocator, pdfPageLabel } from "$lib/reader/pdf-locator";
  import { ANNOTATION_LOCATOR_KIND } from "$lib/api/generated/codes";

  let {
    loaders = { list: fetchAnnotationLibrary, books: fetchAnnotationLibraryBooks },
  }: { loaders?: AnnotationLibraryLoaders } = $props();

  const ALL = "";
  const PAGE_SIZE = 200;
  const SEARCH_DEBOUNCE_MS = 250;

  let search = $state("");
  let bookFilter = $state(ALL);
  let colorFilter = $state(ALL);
  let notesOnly = $state(false);
  let books = $state.raw<AnnotationLibraryBook[]>([]);
  let items = $state.raw<AnnotationLibraryItem[]>([]);
  let totalCount = $state(0);
  let nextCursor = $state<string | null>(null);
  let loading = $state(true);
  let error = $state<string | null>(null);
  let requestSeq = 0;
  let searchTimer: ReturnType<typeof setTimeout> | null = null;

  const bookOptions = $derived([
    { value: ALL, label: "All books" },
    ...books.map((book) => ({ value: book.bookId, label: `${book.title} (${book.highlightCount})` })),
  ]);
  const colorOptions = [
    { value: ALL, label: "All colours" },
    ...ANNOTATION_COLOR_ORDER.map((code) => ({ value: code, label: ANNOTATION_COLOR_LABEL[code] })),
  ];
  const groups = $derived(groupByBook(items));
  const hasFilters = $derived(Boolean(search.trim() || bookFilter || colorFilter || notesOnly));
  const noteCount = $derived(books.reduce((sum, book) => sum + Number(book.noteCount), 0));
  const highlightCount = $derived(books.reduce((sum, book) => sum + Number(book.highlightCount), 0));

  function groupByBook(list: readonly AnnotationLibraryItem[]) {
    const byBook = new Map<string, { bookId: string; title: string; items: AnnotationLibraryItem[] }>();
    for (const item of list) {
      const group = byBook.get(item.bookId) ?? { bookId: item.bookId, title: item.bookTitle, items: [] };
      group.items.push(item);
      byBook.set(item.bookId, group);
    }
    return [...byBook.values()];
  }

  async function load(append = false) {
    const seq = ++requestSeq;
    loading = true;
    error = null;
    try {
      const page = await loaders.list({
        search,
        bookId: bookFilter || undefined,
        color: colorFilter || undefined,
        notesOnly,
        cursor: append ? nextCursor ?? undefined : undefined,
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

  function readerHref(item: AnnotationLibraryItem) {
    return bookReaderHref({ bookId: item.bookId, kind: "book", id: item.bookId, location: item.locator });
  }

  /** "p. N" for a PDF-kind item's 1-based page, or the reading-position percentage for any other kind. */
  function locationLabel(item: AnnotationLibraryItem): string {
    if (item.locatorKind === ANNOTATION_LOCATOR_KIND.pdf) {
      const locator = parsePdfLocator(item.locator);
      if (locator) return `p. ${pdfPageLabel(locator)}`;
    }
    return `${Math.round(Number(item.position) * 100)}%`;
  }

  onMount(() => {
    void loaders.books().then((response) => (books = response.books)).catch(() => undefined);
    reload();
    return () => {
      if (searchTimer) clearTimeout(searchTimer);
    };
  });
</script>

<section class="space-y-4 pb-6">
  <header class="flex flex-col gap-3 border-l-2 border-[var(--color-material-spectrum-yellow)] pl-3 lg:flex-row lg:items-end lg:justify-between">
    <div class="min-w-0">
      <h1 class="flex items-center gap-2 font-heading text-xl font-semibold tracking-tight text-text-primary">
        <Highlighter class="h-5 w-5 text-text-muted" aria-hidden="true" />
        Annotations
      </h1>
      <p class="mt-1 max-w-2xl text-sm leading-relaxed text-text-muted">
        {highlightCount} highlights and {noteCount} notes across {books.length} books.
      </p>
    </div>

    <div class="flex flex-wrap items-center gap-2" role="group" aria-label="Export annotations">
      <Download class="h-4 w-4 text-text-muted" aria-hidden="true" />
      <a class={buttonVariants({ variant: "outline", size: "sm" })} href={annotationExportUrl(ANNOTATION_EXPORT_FORMAT.markdown, bookFilter || undefined)} download>Markdown</a>
      <a class={buttonVariants({ variant: "outline", size: "sm" })} href={annotationExportUrl(ANNOTATION_EXPORT_FORMAT.json, bookFilter || undefined)} download>JSON</a>
      <a class={buttonVariants({ variant: "outline", size: "sm" })} href={annotationExportUrl(ANNOTATION_EXPORT_FORMAT.csv, bookFilter || undefined)} download>CSV</a>
    </div>
  </header>

  <div class="flex flex-col gap-2 sm:flex-row sm:flex-wrap sm:items-center">
    <SearchInput
      class="sm:w-80"
      ariaLabel="Search highlights and notes"
      placeholder="Search highlights and notes"
      bind:value={search}
      oninput={onSearchInput}
      onClear={reload}
    />
    <Select size="sm" value={bookFilter} options={bookOptions} ariaLabel="Filter by book"
      onchange={(value) => { bookFilter = value; reload(); }} />
    <Select size="sm" value={colorFilter} options={colorOptions} ariaLabel="Filter by colour"
      onchange={(value) => { colorFilter = value; reload(); }} />
    <label class="flex items-center gap-2 text-sm text-text-secondary">
      <Toggle size="sm" checked={notesOnly} ariaLabel="Only highlights with notes"
        onchange={(checked) => { notesOnly = checked; reload(); }} />
      With notes
    </label>
  </div>

  {#if error}
    <div class="surface-panel border-l-2 border-error px-3 py-2 text-sm text-error-text" role="alert">{error}</div>
  {:else if !loading && items.length === 0}
    <Empty.Root>
      <Empty.Header>
        <Empty.Media variant="icon"><Highlighter /></Empty.Media>
        <Empty.Title>{hasFilters ? "No matching highlights" : "No highlights yet"}</Empty.Title>
        <Empty.Description>
          {hasFilters
            ? "Try a different search or clear the filters."
            : "Select text while reading an EPUB or PDF and choose Highlight or Add Note."}
        </Empty.Description>
      </Empty.Header>
    </Empty.Root>
  {:else}
    <p class="font-mono text-[0.62rem] uppercase tracking-[0.14em] text-text-muted" aria-live="polite">
      {totalCount} {totalCount === 1 ? "highlight" : "highlights"}
    </p>
    {#each groups as group (group.bookId)}
      {@const summary = books.find((book) => book.bookId === group.bookId)}
      <section class="surface-panel space-y-2 p-3" aria-label={group.title}>
        <h2 class="flex flex-wrap items-baseline gap-2 font-heading text-base font-semibold text-text-primary">
          {group.title}
          {#if summary}
            <span class="font-mono text-[0.62rem] font-normal uppercase tracking-[0.14em] text-text-muted">
              {summary.highlightCount} highlights · {summary.noteCount} notes
            </span>
          {/if}
        </h2>
        <ul class="flex flex-col divide-y divide-border-subtle">
          {#each group.items as item (item.id)}
            <li>
              <a href={readerHref(item)} class="flex gap-3 rounded-sm px-1 py-2 hover:bg-surface-2 focus-visible:outline-2">
                <span class="mt-1 h-4 w-1 shrink-0 rounded-xs" style:background-color={annotationColorPaint(item.color)}
                  aria-label={`${ANNOTATION_COLOR_LABEL[item.color as keyof typeof ANNOTATION_COLOR_LABEL] ?? item.color} highlight`}></span>
                <span class="min-w-0 flex-1 space-y-1">
                  <span class="block text-sm text-text-primary">“{item.selectedText}”</span>
                  {#if item.note}
                    <span class="flex items-start gap-1.5 text-sm text-text-secondary">
                      <NotebookPen class="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden="true" />
                      <span>{item.note}</span>
                    </span>
                  {/if}
                  <span class="flex flex-wrap items-center gap-2 font-mono text-[0.6rem] uppercase tracking-[0.14em] text-text-muted">
                    {locationLabel(item)}
                    {#if item.isStale}
                      <Badge variant="outline" title="The book's file changed since this highlight was made">File changed</Badge>
                    {/if}
                  </span>
                </span>
              </a>
            </li>
          {/each}
        </ul>
      </section>
    {/each}
    {#if nextCursor}
      <Button variant="outline" size="sm" type="button" disabled={loading} onclick={() => load(true)}>Load more</Button>
    {/if}
  {/if}
</section>

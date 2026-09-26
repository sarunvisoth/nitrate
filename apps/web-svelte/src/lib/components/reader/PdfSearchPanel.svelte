<script lang="ts">
  import { Button, Sheet, SearchInput } from "@prismedia/ui-svelte";
  import { X } from "@lucide/svelte";
  import { PdfTextCache, searchPdfDocument, type PdfLikeDocument, type PdfSearchMatch } from "$lib/reader/pdf-text-search";

  interface Props {
    open: boolean;
    /** The pdf.js document proxy; null while the document is still loading. */
    pdf: PdfLikeDocument | null;
    onClose: () => void;
    onNavigate: (page: number) => void;
  }

  let { open, pdf, onClose, onNavigate }: Props = $props();

  let query = $state("");
  let matches = $state<PdfSearchMatch[]>([]);
  let searching = $state(false);
  let searchToken = 0;
  let cache: PdfTextCache | null = null;

  $effect(() => {
    if (!open) clear();
  });

  $effect(() => {
    if (pdf) cache = new PdfTextCache(pdf);
  });

  function clear() {
    query = "";
    matches = [];
    searching = false;
    searchToken++;
  }

  async function runSearch() {
    const trimmed = query.trim();
    if (!trimmed || !pdf || !cache) {
      matches = [];
      return;
    }

    const token = ++searchToken;
    searching = true;
    const next: PdfSearchMatch[] = [];
    try {
      for await (const match of searchPdfDocument(pdf, trimmed, cache)) {
        if (token !== searchToken) return; // a newer search superseded this one
        next.push(match);
        matches = [...next];
      }
    } finally {
      if (token === searchToken) searching = false;
    }
  }

  function select(match: PdfSearchMatch) {
    onNavigate(match.page);
    onClose();
  }
</script>

<Sheet.Root {open} onOpenChange={(next) => { if (!next) onClose(); }}>
  {#if open}
    <Sheet.Content side="left" showCloseButton={false} class="w-[min(22rem,86vw)] gap-0 sm:max-w-[22rem]">
      <Sheet.Header class="flex-row items-center justify-between border-b px-4 pt-[max(0.75rem,env(safe-area-inset-top))] pb-3">
        <Sheet.Title>Search document</Sheet.Title>
        <Button variant="ghost" size="icon" aria-label="Close search" onclick={onClose}><X /></Button>
      </Sheet.Header>
      <div class="flex items-center gap-2 border-b p-3">
        <SearchInput
          class="flex-1"
          bind:value={query}
          ariaLabel="Search this document"
          placeholder="Search this document"
          onClear={clear}
          onkeydown={(event: KeyboardEvent) => { if (event.key === "Enter") void runSearch(); }}
        />
        <Button size="sm" type="button" onclick={() => runSearch()}>Search</Button>
        {#if matches.length > 0 || query}
          <Button variant="ghost" size="sm" type="button" onclick={clear}>Clear</Button>
        {/if}
      </div>
      <div class="min-h-0 flex-1 overflow-y-auto p-2 pb-[max(1rem,env(safe-area-inset-bottom))]">
        {#if searching && matches.length === 0}
          <p class="p-2 text-label text-muted-foreground">Searching…</p>
        {:else if query.trim() && matches.length === 0 && !searching}
          <p class="p-2 text-label text-muted-foreground">No matches found.</p>
        {:else}
          {#each matches as match, index (`${match.page}-${index}`)}
            <Button
              variant="ghost"
              class="block h-auto w-full justify-start whitespace-normal rounded-md p-2 text-left text-label font-normal"
              type="button"
              onclick={() => select(match)}
            >
              <span class="mb-0.5 block font-mono text-[0.62rem] uppercase tracking-[0.14em] text-text-muted">
                p. {match.page + 1}
              </span>
              {match.excerpt.pre}<mark class="bg-transparent font-semibold text-foreground">{match.excerpt.match}</mark>{match.excerpt.post}
            </Button>
          {/each}
        {/if}
      </div>
    </Sheet.Content>
  {/if}
</Sheet.Root>

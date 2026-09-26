<script lang="ts">
  import { Button, Sheet, SearchInput } from "@prismedia/ui-svelte";
  import { X } from "@lucide/svelte";

  interface SearchExcerpt {
    cfi: string;
    excerpt: { pre: string; match: string; post: string };
  }

  interface SearchGroup {
    label: string;
    items: SearchExcerpt[];
  }

  interface Props {
    open: boolean;
    /** The foliate `<foliate-view>` element; has no published types (vendored, client-only). */
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    view: any;
    onClose: () => void;
    onNavigate: (cfi: string) => void;
  }

  let { open, view, onClose, onNavigate }: Props = $props();

  let query = $state("");
  let groups = $state<SearchGroup[]>([]);
  let searching = $state(false);
  let searchToken = 0;

  $effect(() => {
    if (!open) {
      clear();
    }
  });

  function clear() {
    query = "";
    groups = [];
    searching = false;
    try {
      view?.clearSearch?.();
    } catch {
      // ignore: the view may have unloaded
    }
  }

  async function runSearch() {
    const trimmed = query.trim();
    if (!trimmed || !view?.search) {
      groups = [];
      return;
    }

    const token = ++searchToken;
    searching = true;
    const next: SearchGroup[] = [];
    try {
      for await (const result of view.search({ query: trimmed })) {
        if (token !== searchToken) return; // a newer search superseded this one
        if (result === "done") break;
        if (result?.subitems) {
          next.push({
            label: result.label || "Untitled section",
            items: result.subitems.map((item: { cfi: string; excerpt: unknown }) => ({
              cfi: item.cfi,
              excerpt: item.excerpt as SearchExcerpt["excerpt"],
            })),
          });
          groups = [...next];
        }
      }
    } finally {
      if (token === searchToken) searching = false;
    }
  }

  function select(cfi: string) {
    onNavigate(cfi);
    onClose();
  }

  const totalMatches = $derived(groups.reduce((sum, group) => sum + group.items.length, 0));
</script>

<Sheet.Root {open} onOpenChange={(next) => { if (!next) onClose(); }}>
  {#if open}
    <Sheet.Content side="left" showCloseButton={false} class="w-[min(22rem,86vw)] gap-0 sm:max-w-[22rem]">
      <Sheet.Header class="flex-row items-center justify-between border-b px-4 pt-[max(0.75rem,env(safe-area-inset-top))] pb-3">
        <Sheet.Title>Search book</Sheet.Title>
        <Button variant="ghost" size="icon" aria-label="Close search" onclick={onClose}><X /></Button>
      </Sheet.Header>
      <div class="flex items-center gap-2 border-b p-3">
        <SearchInput
          class="flex-1"
          bind:value={query}
          ariaLabel="Search this book"
          placeholder="Search this book"
          onClear={clear}
          onkeydown={(event: KeyboardEvent) => { if (event.key === "Enter") void runSearch(); }}
        />
        <Button size="sm" type="button" onclick={() => runSearch()}>Search</Button>
        {#if groups.length > 0 || query}
          <Button variant="ghost" size="sm" type="button" onclick={clear}>Clear</Button>
        {/if}
      </div>
      <div class="min-h-0 flex-1 overflow-y-auto p-2 pb-[max(1rem,env(safe-area-inset-bottom))]">
        {#if searching}
          <p class="p-2 text-label text-muted-foreground">Searching…</p>
        {:else if query.trim() && totalMatches === 0}
          <p class="p-2 text-label text-muted-foreground">No matches found.</p>
        {:else}
          {#each groups as group (group.label)}
            <p class="px-2 pt-2 text-label font-medium text-muted-foreground">{group.label}</p>
            {#each group.items as item (item.cfi)}
              <Button
                variant="ghost"
                class="block h-auto w-full justify-start whitespace-normal rounded-md p-2 text-left text-label font-normal"
                type="button"
                onclick={() => select(item.cfi)}
              >
                {item.excerpt.pre}<mark class="bg-transparent font-semibold text-foreground">{item.excerpt.match}</mark>{item.excerpt.post}
              </Button>
            {/each}
          {/each}
        {/if}
      </div>
    </Sheet.Content>
  {/if}
</Sheet.Root>

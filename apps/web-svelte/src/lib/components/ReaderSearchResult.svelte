<script lang="ts">
  import { Badge, Command } from "@prismedia/ui-svelte";
  import { BookMarked, Highlighter } from "@lucide/svelte";
  import type { ReaderSearchResultItem } from "$lib/search/reader-search";

  let { item, onSelect }: { item: ReaderSearchResultItem; onSelect: (href: string) => void } = $props();
</script>

<Command.Item value={`${item.kind}:${item.id}`} showIndicator={false} onSelect={() => onSelect(item.href)} class="gap-3">
  <div class="flex size-10 shrink-0 items-center justify-center rounded-sm bg-surface-2 text-muted-foreground" aria-hidden="true">
    {#if item.kind === "annotation"}<Highlighter class="size-4" />{:else}<BookMarked class="size-4" />{/if}
  </div>
  <span class="flex min-w-0 flex-1 flex-col gap-1">
    <span class="truncate font-medium text-foreground">{item.title}</span>
    {#if item.snippet}<span class="truncate text-xs text-muted-foreground">{item.snippet}</span>{/if}
  </span>
  {#if item.kind === "annotation" && item.meta}<Badge>{item.meta}</Badge>{/if}
</Command.Item>

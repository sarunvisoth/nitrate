<script lang="ts">
  import { Badge, buttonVariants } from "@prismedia/ui-svelte";
  import { BookMarked, Highlighter } from "@lucide/svelte";
  import { buildHrefWithFrom } from "$lib/back-navigation";
  import type { ReaderSearchGroup } from "$lib/search/reader-search";

  let { group, currentPath }: { group: ReaderSearchGroup; currentPath: string } = $props();
  const titleId = $props.id();
</script>

<section aria-labelledby={titleId} class="flex min-w-0 flex-col gap-3">
  <header class="flex items-center justify-between gap-3">
    <h2 id={titleId} class="flex items-center gap-2 font-heading text-base font-medium">
      {#if group.kind === "annotation"}<Highlighter class="size-4" aria-hidden="true" />{:else}<BookMarked class="size-4" aria-hidden="true" />{/if}
      {group.label}
    </h2>
    <a href={group.seeAllHref} class={buttonVariants({ variant: "ghost", size: "sm" })}>See all {group.label.toLowerCase()}</a>
  </header>
  <ul class="flex flex-col gap-1">
    {#each group.items as item (item.id)}
      <li>
        <a href={buildHrefWithFrom(item.href, currentPath)}
          class="flex min-h-11 min-w-0 items-center gap-3 rounded-sm px-2 py-2 hover:bg-surface-2 focus-visible:outline-2">
          <span class="flex min-w-0 flex-1 flex-col gap-0.5">
            <span class="truncate font-medium text-foreground">{item.title}</span>
            {#if item.snippet}<span class="truncate text-xs text-muted-foreground">{item.snippet}</span>{/if}
          </span>
          {#if group.kind === "annotation" && item.meta}<Badge>{item.meta}</Badge>{/if}
        </a>
      </li>
    {/each}
  </ul>
</section>

<script lang="ts">
  import type { Snippet } from "svelte";
  import { Button, Sheet, Tabs } from "@prismedia/ui-svelte";
  import { X } from "@lucide/svelte";

  let closeButton = $state<HTMLButtonElement | null>(null);
  let {
    open,
    onClose,
    children,
    bookmarks,
    bookmarkCount = 0,
  }: {
    open: boolean;
    onClose: () => void;
    children: Snippet;
    /** Optional bookmarks list; when given, the sheet shows Contents | Bookmarks tabs. */
    bookmarks?: Snippet;
    bookmarkCount?: number;
  } = $props();

  let tab = $state("contents");
</script>

<Sheet.Root {open} onOpenChange={next => { if (!next) onClose(); }}>
  {#if open}
    <Sheet.Content onOpenAutoFocus={event => { event.preventDefault(); closeButton?.focus(); }} side="left" showCloseButton={false} class="w-[min(22rem,86vw)] gap-0 sm:max-w-[22rem]">
      <Sheet.Header class="flex-row items-center justify-between border-b px-4 pt-[max(0.75rem,env(safe-area-inset-top))] pb-3">
        <Sheet.Title>Contents</Sheet.Title>
        <Button variant="ghost" size="icon" bind:ref={closeButton} aria-label="Close contents" onclick={onClose}><X /></Button>
      </Sheet.Header>
      {#if bookmarks}
        <Tabs.Root bind:value={tab} class="min-h-0 flex-1 gap-0">
          <Tabs.List variant="line" class="w-full justify-start rounded-none border-b px-2" aria-label="Contents sections">
            <Tabs.Trigger value="contents">Chapters</Tabs.Trigger>
            <Tabs.Trigger value="bookmarks">Bookmarks{bookmarkCount ? ` (${bookmarkCount})` : ""}</Tabs.Trigger>
          </Tabs.List>
          <Tabs.Content value="contents" class="min-h-0 flex-1 overflow-y-auto">
            <nav aria-label="Table of contents" class="p-2 pb-[max(1rem,env(safe-area-inset-bottom))]">
              {@render children()}
            </nav>
          </Tabs.Content>
          <Tabs.Content value="bookmarks" class="min-h-0 flex-1 overflow-y-auto">
            <div aria-label="Bookmarks" role="region" class="p-2 pb-[max(1rem,env(safe-area-inset-bottom))]">
              {@render bookmarks()}
            </div>
          </Tabs.Content>
        </Tabs.Root>
      {:else}
        <nav aria-label="Table of contents" class="min-h-0 flex-1 overflow-y-auto p-2 pb-[max(1rem,env(safe-area-inset-bottom))]">
          {@render children()}
        </nav>
      {/if}
    </Sheet.Content>
  {/if}
</Sheet.Root>

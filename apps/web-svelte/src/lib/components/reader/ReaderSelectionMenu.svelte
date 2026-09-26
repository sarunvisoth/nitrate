<script lang="ts" module>
  import type { Component } from "svelte";

  /** One entry in the reader's text-selection menu (Highlight, Add Note, and later Define, Translate, …). */
  export interface ReaderSelectionAction {
    id: string;
    label: string;
    icon?: Component;
    run: () => void;
  }
</script>

<script lang="ts">
  import { Button, Popover } from "@prismedia/ui-svelte";
  import { Search } from "@lucide/svelte";
  import { measurableFromRect } from "$lib/reader/epub-selection";
  import { buildLookupActions, searchWebUrl, type LookupSourceContext } from "$lib/reader/reader-lookup-actions";
  import LookupPopover from "./LookupPopover.svelte";

  interface Props {
    /** Viewport rect of the current selection; null hides the menu. */
    anchor: { left: number; top: number; width: number; height: number } | null;
    /** The currently selected text, used to decide which lookup actions apply and what to look up. */
    selectedText: string;
    actions: readonly ReaderSelectionAction[];
    onDismiss: () => void;
    /** Where this selection lives in the book, for Define's "Add to Vocabulary". */
    sourceContext?: LookupSourceContext | null;
  }

  let { anchor, selectedText = "", actions, onDismiss, sourceContext = null }: Props = $props();

  const customAnchor = $derived(anchor ? measurableFromRect(anchor) : null);

  // Define/Translate/Wikipedia share one anchored lookup popover, opened over the same selection;
  // Search web opens DuckDuckGo directly and dismisses the menu.
  let lookupMode = $state<"define" | "translate" | "wikipedia" | null>(null);

  const lookupActions = $derived(
    buildLookupActions(selectedText, {
      onDefine: () => (lookupMode = "define"),
      onTranslate: () => (lookupMode = "translate"),
      onWikipedia: () => (lookupMode = "wikipedia"),
    }),
  );

  function searchWeb() {
    const text = selectedText;
    onDismiss();
    if (text.trim()) window.open(searchWebUrl(text), "_blank", "noopener");
  }
</script>

<Popover.Root open={anchor !== null && lookupMode === null} onOpenChange={(next) => { if (!next) onDismiss(); }}>
  {#if customAnchor}
    <Popover.Content
      {customAnchor}
      portalProps={{ disabled: true }}
      side="top"
      sideOffset={8}
      class="w-auto flex-row flex-wrap gap-1 p-1"
      aria-label="Selection actions"
      onOpenAutoFocus={(event) => event.preventDefault()}
    >
      {#each actions as action (action.id)}
        <Button variant="ghost" size="sm" type="button" onclick={() => action.run()}>
          {#if action.icon}
            <action.icon />
          {/if}
          {action.label}
        </Button>
      {/each}
      {#each lookupActions as action (action.id)}
        <Button variant="ghost" size="sm" type="button" onclick={() => action.run()}>
          <action.icon />
          {action.label}
        </Button>
      {/each}
      <Button variant="ghost" size="sm" type="button" onclick={searchWeb}>
        <Search />
        Search web
      </Button>
    </Popover.Content>
  {/if}
</Popover.Root>

<LookupPopover
  anchor={lookupMode ? anchor : null}
  term={selectedText}
  initialMode={lookupMode ?? "define"}
  onDismiss={() => { lookupMode = null; onDismiss(); }}
  {sourceContext}
/>

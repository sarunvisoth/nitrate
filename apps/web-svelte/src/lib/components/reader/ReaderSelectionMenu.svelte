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
  import { measurableFromRect } from "$lib/reader/epub-selection";

  interface Props {
    /** Viewport rect of the current selection; null hides the menu. */
    anchor: { left: number; top: number; width: number; height: number } | null;
    actions: readonly ReaderSelectionAction[];
    onDismiss: () => void;
  }

  let { anchor, actions, onDismiss }: Props = $props();

  const customAnchor = $derived(anchor ? measurableFromRect(anchor) : null);
</script>

<Popover.Root open={anchor !== null} onOpenChange={(next) => { if (!next) onDismiss(); }}>
  {#if customAnchor}
    <Popover.Content
      {customAnchor}
      portalProps={{ disabled: true }}
      side="top"
      sideOffset={8}
      class="w-auto flex-row gap-1 p-1"
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
    </Popover.Content>
  {/if}
</Popover.Root>

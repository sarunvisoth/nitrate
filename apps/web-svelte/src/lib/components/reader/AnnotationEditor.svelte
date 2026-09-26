<script lang="ts">
  import { Button, Popover, Textarea } from "@prismedia/ui-svelte";
  import { Trash2 } from "@lucide/svelte";
  import {
    ANNOTATION_COLOR_LABEL,
    ANNOTATION_COLOR_ORDER,
    annotationColorPaint,
  } from "$lib/reader/annotation-colors";
  import { measurableFromRect } from "$lib/reader/epub-selection";
  import type { AnnotationColorCode } from "$lib/api/generated/codes";

  interface Props {
    /** Viewport rect of the highlight being edited; null hides the editor. */
    anchor: { left: number; top: number; width: number; height: number } | null;
    color: string;
    note: string | null;
    /** Focus the note field on open (used by "Add Note"). */
    focusNote?: boolean;
    onChangeColor: (color: AnnotationColorCode) => void;
    onSaveNote: (note: string) => void;
    onDelete: () => void;
    onDismiss: () => void;
  }

  let {
    anchor,
    color,
    note,
    focusNote = false,
    onChangeColor,
    onSaveNote,
    onDelete,
    onDismiss,
  }: Props = $props();

  let draft = $state("");
  let noteField = $state<HTMLTextAreaElement | null>(null);
  const customAnchor = $derived(anchor ? measurableFromRect(anchor) : null);

  $effect(() => {
    if (anchor) draft = note ?? "";
  });
</script>

<Popover.Root open={anchor !== null} onOpenChange={(next) => { if (!next) onDismiss(); }}>
  {#if customAnchor}
    <Popover.Content
      {customAnchor}
      portalProps={{ disabled: true }}
      side="bottom"
      sideOffset={8}
      class="w-72 gap-3 p-3"
      aria-label="Edit highlight"
      onOpenAutoFocus={(event) => {
        event.preventDefault();
        if (focusNote) noteField?.focus();
      }}
    >
      <div class="flex items-center gap-1" role="group" aria-label="Highlight colour">
        {#each ANNOTATION_COLOR_ORDER as code (code)}
          <Button
            variant="ghost"
            size="icon-sm"
            type="button"
            aria-label={ANNOTATION_COLOR_LABEL[code]}
            aria-pressed={color === code}
            onclick={() => onChangeColor(code)}
          >
            <span
              class="annotation-swatch"
              class:annotation-swatch-selected={color === code}
              style:background-color={annotationColorPaint(code)}
            ></span>
          </Button>
        {/each}
      </div>
      <Textarea bind:ref={noteField} bind:value={draft} rows={3} placeholder="Add a note" aria-label="Note" />
      <div class="flex items-center justify-between gap-2">
        <Button variant="ghost" size="sm" type="button" onclick={onDelete}>
          <Trash2 />
          Delete highlight
        </Button>
        <Button size="sm" type="button" onclick={() => onSaveNote(draft)}>Save note</Button>
      </div>
    </Popover.Content>
  {/if}
</Popover.Root>

<style>
  .annotation-swatch {
    display: block;
    width: 1rem;
    height: 1rem;
    border-radius: var(--radius-xs, 4px);
    opacity: 0.85;
  }

  .annotation-swatch-selected {
    outline: 2px solid currentColor;
    outline-offset: 2px;
    opacity: 1;
  }
</style>

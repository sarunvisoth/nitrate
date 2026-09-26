<script lang="ts">
  import { parsePdfLocator } from "$lib/reader/pdf-locator";
  import { annotationColorPaint } from "$lib/reader/annotation-colors";
  import type { BookAnnotation } from "$lib/api/generated/model";

  interface Props {
    /** 0-based page index this layer overlays. */
    page: number;
    /** Non-deleted PDF-kind annotations for the whole document; only this page's are drawn. */
    annotations: readonly BookAnnotation[];
    onTap: (annotation: BookAnnotation, anchor: { left: number; top: number; width: number; height: number }) => void;
  }

  let { page, annotations, onTap }: Props = $props();

  interface DrawnRect {
    annotation: BookAnnotation;
    left: number;
    top: number;
    width: number;
    height: number;
  }

  const rects = $derived(
    annotations.flatMap((annotation): DrawnRect[] => {
      const locator = parsePdfLocator(annotation.locator);
      if (!locator || locator.page !== page) return [];
      return locator.rects.map((rect) => ({
        annotation,
        left: rect.x * 100,
        top: rect.y * 100,
        width: rect.w * 100,
        height: rect.h * 100,
      }));
    }),
  );

  function handleTap(event: MouseEvent, drawn: DrawnRect) {
    const target = event.currentTarget as HTMLElement;
    const parent = target.offsetParent as HTMLElement | null;
    const rect = target.getBoundingClientRect();
    const containerRect = parent?.getBoundingClientRect();
    onTap(drawn.annotation, {
      left: rect.left - (containerRect?.left ?? 0),
      top: rect.top - (containerRect?.top ?? 0),
      width: rect.width,
      height: rect.height,
    });
  }
</script>

{#if rects.length > 0}
  <div class="pdf-highlight-layer">
    {#each rects as drawn, index (drawn.annotation.id + "-" + index)}
      <!-- svelte-ignore a11y_click_events_have_key_events, a11y_no_static_element_interactions -->
      <div
        class="pdf-highlight-rect"
        style:left={`${drawn.left}%`}
        style:top={`${drawn.top}%`}
        style:width={`${drawn.width}%`}
        style:height={`${drawn.height}%`}
        style:background-color={annotationColorPaint(drawn.annotation.color)}
        role="button"
        tabindex="0"
        aria-label={drawn.annotation.note ? "Edit highlight with note" : "Edit highlight"}
        onclick={(event) => handleTap(event, drawn)}
      ></div>
    {/each}
  </div>
{/if}

<style>
  .pdf-highlight-layer {
    position: absolute;
    inset: 0;
    z-index: 4;
    pointer-events: none;
  }

  .pdf-highlight-rect {
    position: absolute;
    opacity: 0.35;
    cursor: pointer;
    pointer-events: auto;
    border-radius: 1px;
  }

  .pdf-highlight-rect:hover,
  .pdf-highlight-rect:focus-visible {
    opacity: 0.5;
    outline: none;
  }
</style>

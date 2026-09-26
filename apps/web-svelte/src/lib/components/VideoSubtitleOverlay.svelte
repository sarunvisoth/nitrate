<script lang="ts">
  import { cn } from "@prismedia/ui-svelte";
  import {
    captionClassName,
    type SubtitleAppearance,
  } from "$lib/player/subtitle-appearance";
  import type { VideoSubtitleTrack } from "$lib/player/subtitle-types";
  import AssSubtitleOverlay from "./AssSubtitleOverlay.svelte";

  interface Props {
    activeCueText?: string | null;
    appearance: SubtitleAppearance;
    assTrack?: VideoSubtitleTrack | null;
    showTextCue?: boolean;
    videoEl?: HTMLVideoElement | null;
  }

  let {
    activeCueText = null,
    appearance,
    assTrack = null,
    showTextCue = false,
    videoEl = null,
  }: Props = $props();
</script>

{#if assTrack}
  {#key assTrack.id}
    <AssSubtitleOverlay
      {videoEl}
      sourceUrl={assTrack.sourceUrl ?? ""}
      opacity={appearance.opacity}
    />
  {/key}
{/if}

{#if activeCueText && showTextCue}
  <div
    class="pointer-events-none absolute inset-x-0 flex justify-center px-4"
    style:top="{appearance.positionPercent}%"
    style:transform="translateY(-100%)"
    style:opacity={appearance.opacity}
  >
    <div
      class={cn(
        captionClassName(appearance.style),
        "max-w-[86%] whitespace-pre-line text-center font-medium leading-snug",
      )}
      style:font-size="{appearance.fontScale * 1.05}rem"
    >
      {activeCueText}
    </div>
  </div>
{/if}

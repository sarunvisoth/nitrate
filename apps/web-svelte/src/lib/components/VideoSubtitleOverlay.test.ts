import { render, screen } from "@testing-library/svelte";
import { describe, expect, it } from "vitest";
import VideoSubtitleOverlay from "./VideoSubtitleOverlay.svelte";
import { defaultSubtitleAppearance } from "$lib/player/subtitle-appearance";

describe("VideoSubtitleOverlay", () => {
  it("suppresses text cues while an ASS renderer owns the active subtitle", () => {
    render(VideoSubtitleOverlay, {
      props: {
        activeCueText: "Hidden caption",
        appearance: defaultSubtitleAppearance,
        showTextCue: false,
      },
    });

    expect(screen.queryByText("Hidden caption")).toBeNull();
  });
});

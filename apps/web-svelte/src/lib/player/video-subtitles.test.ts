import { afterEach, describe, expect, it, vi } from "vitest";
import { fetchVideoSubtitleCues, parseWebVttCues } from "./video-subtitles";

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("video-subtitles", () => {
  it("parses WebVTT subtitle assets into transcript cues", () => {
    expect(
      parseWebVttCues(`WEBVTT

intro
00:00.500 --> 00:02.000 align:center
<v Speaker>Hello there.</v>

00:03:04.250 --> 00:03:05.500
Second line
still second line
`),
    ).toEqual([
      { start: 0.5, end: 2, text: "Hello there." },
      { start: 184.25, end: 185.5, text: "Second line\nstill second line" },
    ]);
  });

  it("fetches fresh cues when a stable track id receives a new content revision", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(new Response(`WEBVTT

00:00.000 --> 00:01.000
Before edit
`))
      .mockResolvedValueOnce(new Response(`WEBVTT

00:00.000 --> 00:01.000
After edit
`));
    vi.stubGlobal("fetch", fetchMock);

    const before = await fetchVideoSubtitleCues({
      id: "stable-track",
      url: "/api/videos/video-1/subtitles/stable-track?v=before",
    });
    const after = await fetchVideoSubtitleCues({
      id: "stable-track",
      url: "/api/videos/video-1/subtitles/stable-track?v=after",
    });

    expect(before.cues[0]?.text).toBe("Before edit");
    expect(after.cues[0]?.text).toBe("After edit");
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it("retries a subtitle fetch after a transient failure", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(new Response(null, { status: 503 }))
      .mockResolvedValueOnce(new Response(`WEBVTT

00:00.000 --> 00:01.000
Recovered
`));
    vi.stubGlobal("fetch", fetchMock);
    const track = {
      id: "retry-track",
      url: "/api/videos/video-1/subtitles/retry-track?v=revision",
    };

    await expect(fetchVideoSubtitleCues(track)).rejects.toThrow("Subtitle cues failed (503)");
    await expect(fetchVideoSubtitleCues(track)).resolves.toEqual({
      cues: [{ start: 0, end: 1, text: "Recovered" }],
    });
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });
});

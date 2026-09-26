import { describe, expect, it } from "vitest";
import { problemMessage, requestInit, unwrapGenerated } from "./generated-response";

describe("generated API response helpers", () => {
  it("unwraps successful generated responses", () => {
    expect(unwrapGenerated({ status: 200, data: { ok: true } }, "fallback")).toEqual({ ok: true });
  });

  it("uses problem detail fields when generated responses fail", () => {
    expect(() => unwrapGenerated({ status: 404, data: { detail: "Missing entity" } }, "fallback"))
      .toThrow("Missing entity");
  });

  it("converts request options to fetch init only when a signal exists", () => {
    const controller = new AbortController();

    expect(requestInit()).toBeUndefined();
    expect(requestInit({ signal: controller.signal })).toEqual({ signal: controller.signal });
  });

  it("ignores blank problem strings", () => {
    expect(problemMessage("  ")).toBeNull();
  });
});

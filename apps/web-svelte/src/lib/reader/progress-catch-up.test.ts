import { describe, expect, it } from "vitest";
import { pdfPositionKey, remoteProgressToAdopt } from "./progress-catch-up";

const saved = "2026-09-26T10:00:00Z";
const later = "2026-09-26T10:05:00Z";

describe("remoteProgressToAdopt", () => {
  it("adopts a newer EPUB CFI from another device", () => {
    const remote = { updatedAt: later, location: "epubcfi(/6/8!/4/2/1:0)", index: 4200, total: 10000 };
    expect(remoteProgressToAdopt(remote, { lastSeenAt: saved, currentLocation: "epubcfi(/6/4!/4/2/1:0)" }))
      .toEqual({ kind: "cfi", cfi: "epubcfi(/6/8!/4/2/1:0)" });
  });

  it("ignores progress that is not newer than this device's last save", () => {
    const remote = { updatedAt: saved, location: "epubcfi(/6/8!/4/2/1:0)", index: 4200, total: 10000 };
    expect(remoteProgressToAdopt(remote, { lastSeenAt: saved, currentLocation: null })).toBeNull();
  });

  it("ignores the same location", () => {
    const cfi = "epubcfi(/6/8!/4/2/1:0)";
    const remote = { updatedAt: later, location: cfi, index: 4200, total: 10000 };
    expect(remoteProgressToAdopt(remote, { lastSeenAt: saved, currentLocation: cfi })).toBeNull();
  });

  it("falls back to a fraction when the remote locator is not a CFI (native app formats)", () => {
    const remote = { updatedAt: later, location: "{\"native\":1}", index: 2500, total: 10000 };
    expect(remoteProgressToAdopt(remote, { lastSeenAt: saved, currentLocation: null }))
      .toEqual({ kind: "fraction", fraction: 0.25 });
  });

  it("returns a zero-based page for PDF progress, unless it is the current page", () => {
    const remote = { updatedAt: later, location: null, index: 11, total: 40 };
    expect(remoteProgressToAdopt(remote, { lastSeenAt: saved, currentLocation: null, pdf: true }))
      .toEqual({ kind: "page", page: 11 });
    expect(remoteProgressToAdopt(remote, { lastSeenAt: saved, currentLocation: null, currentPage: 11, pdf: true }))
      .toBeNull();
  });

  it("ignores positions this device saved itself (the server echoing our own save)", () => {
    const cfi = "epubcfi(/6/8!/4/2/1:0)";
    const remote = { updatedAt: later, location: cfi, index: 4200, total: 10000 };
    expect(remoteProgressToAdopt(remote, {
      lastSeenAt: saved,
      currentLocation: "epubcfi(/6/10!/4/2/1:0)",
      ownPositions: new Set([cfi]),
    })).toBeNull();
    const pdfRemote = { updatedAt: later, location: null, index: 5, total: 40 };
    expect(remoteProgressToAdopt(pdfRemote, {
      lastSeenAt: saved,
      currentLocation: null,
      currentPage: 6,
      ownPositions: new Set([pdfPositionKey(5)]),
      pdf: true,
    })).toBeNull();
  });

  it("ignores missing or unparseable clocks", () => {
    const remote = { updatedAt: null, location: "epubcfi(/6/8!/4/2/1:0)", index: 1, total: 2 };
    expect(remoteProgressToAdopt(remote, { lastSeenAt: saved, currentLocation: null })).toBeNull();
  });
});

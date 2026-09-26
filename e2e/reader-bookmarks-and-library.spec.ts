import { test, expect, type APIRequestContext, type Page } from "@playwright/test";
import { readFile } from "node:fs/promises";
import { signInContext } from "./auth";
import { ensureAnnotationFixtureBook, resetBookProgress } from "./book-fixture";
import { bookSectionFrame, revealReaderChrome, selectInFrame } from "./reader-helpers";
import { CAPABILITY_KIND } from "../apps/web-svelte/src/lib/api/generated/codes";

// Shares the account and fixture book with reader-annotations.spec.ts: run with --workers=1.
test.describe.configure({ mode: "serial" });

const PHRASE = "the fishermen counted their nets";
const PLAIN = "A gull argued with the wind";
const NOTE = "Nets are a metaphor for memory";

async function clearLeftovers(request: APIRequestContext, token: string, bookId: string) {
  const auth = { Authorization: `Bearer ${token}` };
  for (const kind of ["annotations", "bookmarks"] as const) {
    const body = await (await request.get(`/api/books/${bookId}/${kind}`, { headers: auth })).json();
    for (const item of (body[kind] as Array<{ id: string; deletedAt: string | null }>).filter((i) => !i.deletedAt)) {
      const removed = await request.delete(
        `/api/books/${bookId}/${kind}/${item.id}?updatedAt=${encodeURIComponent(new Date().toISOString())}`,
        { headers: auth },
      );
      expect((await removed.json()).deletedAt).not.toBeNull();
    }
  }
}

async function openReaderScrolled(page: Page, bookId: string) {
  const bookmarksLoaded = page.waitForResponse(
    (r) => r.url().includes(`/api/books/${bookId}/bookmarks`) && r.request().method() === "GET",
  );
  await page.goto(`/books/${bookId}/reader`);
  const scrolled = page.getByRole("button", { name: "Scrolled reading" });
  await expect(scrolled).toBeVisible({ timeout: 30_000 });
  if ((await scrolled.getAttribute("aria-pressed")) !== "true") await scrolled.click();
  await bookmarksLoaded;
}

/** Mean RGB of a screenshot region, decoded in the browser (no image dependency needed). */
async function meanColour(page: Page, clip: { x: number; y: number; width: number; height: number }) {
  const png = await page.screenshot({ clip });
  return page.evaluate(async (b64) => {
    const img = new Image();
    img.src = `data:image/png;base64,${b64}`;
    await img.decode();
    const canvas = document.createElement("canvas");
    canvas.width = img.width;
    canvas.height = img.height;
    const ctx = canvas.getContext("2d")!;
    ctx.drawImage(img, 0, 0);
    const data = ctx.getImageData(0, 0, img.width, img.height).data;
    let r = 0, g = 0, b = 0;
    for (let i = 0; i < data.length; i += 4) { r += data[i]!; g += data[i + 1]!; b += data[i + 2]!; }
    const n = data.length / 4;
    return { r: r / n, g: g / n, b: b / n };
  }, png.toString("base64"));
}

async function textRect(page: Page, text: string) {
  const frame = await bookSectionFrame(page, text);
  return frame.evaluate((t) => {
    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
    let node: Node | null;
    while ((node = walker.nextNode())) {
      const at = node.textContent?.indexOf(t) ?? -1;
      if (at < 0) continue;
      const range = document.createRange();
      range.setStart(node, at);
      range.setEnd(node, at + t.length);
      const r = range.getClientRects()[0]!;
      const f = window.frameElement?.getBoundingClientRect() ?? { left: 0, top: 0 };
      return { x: Math.round(f.left + r.left), y: Math.round(f.top + r.top), width: Math.round(r.width), height: Math.round(r.height) };
    }
    throw new Error(`text not found: ${t}`);
  }, text);
}

test("bookmarks, painted highlights with notes, the /annotations dashboard, and CSV export", async ({
  browser,
  request,
  baseURL,
}) => {
  test.setTimeout(180_000);
  const context = await browser.newContext({ acceptDownloads: true });
  const token = await signInContext(context, request, baseURL!);
  const bookId = await ensureAnnotationFixtureBook(request, token);
  await resetBookProgress(request, token, bookId);
  await clearLeftovers(request, token, bookId);
  const page = await context.newPage();

  // 1. Bookmark the current page; it survives a reload and can be jumped to from the Bookmarks tab.
  await openReaderScrolled(page, bookId);
  await revealReaderChrome(page);
  const bookmarkSaved = page.waitForResponse(
    (r) => r.url().includes(`/api/books/${bookId}/bookmarks/`) && r.request().method() === "PUT",
  );
  await page.getByRole("button", { name: "Bookmark this page" }).click();
  expect((await (await bookmarkSaved).json()).chapterLabel).toBe("The Harbour");
  await expect(page.getByRole("button", { name: "Remove bookmark" })).toBeVisible();

  await openReaderScrolled(page, bookId);
  await revealReaderChrome(page);
  await page.getByRole("button", { name: "Table of contents" }).click();
  await page.getByRole("tab", { name: "Bookmarks (1)" }).click();
  await page.getByRole("button", { name: /The Harbour/ }).click();
  await expect(page.getByRole("dialog", { name: "Contents" })).toBeHidden();

  // 2. Highlight with a note; the highlight is actually painted (yellow tint behind the text).
  const frame = await bookSectionFrame(page, PHRASE);
  await selectInFrame(frame, PHRASE);
  const created = page.waitForResponse(
    (r) => r.url().includes(`/api/books/${bookId}/annotations/`) && r.request().method() === "PUT",
  );
  await page.getByRole("button", { name: "Add Note" }).click();
  await created;
  const noted = page.waitForResponse(
    (r) => r.url().includes(`/api/books/${bookId}/annotations/`) && r.request().method() === "PUT"
      && r.request().postDataJSON()?.note === NOTE,
  );
  await page.getByRole("textbox", { name: "Note" }).fill(NOTE);
  await page.getByRole("button", { name: "Save note" }).click();
  await noted;

  const highlighted = await textRect(page, PHRASE);
  const plain = await textRect(page, PLAIN);
  await expect
    .poll(async () => {
      const lit = await meanColour(page, highlighted);
      const base = await meanColour(page, { ...plain, width: highlighted.width, height: highlighted.height });
      // Yellow at ~30% over the dark page lifts red and green far more than blue.
      return (lit.r + lit.g) / 2 - lit.b - ((base.r + base.g) / 2 - base.b);
    }, { timeout: 15_000 })
    .toBeGreaterThan(15);

  // 3. The dashboard finds it by its note, and the colour filter narrows it out.
  await page.goto("/annotations");
  const search = page.getByRole("searchbox", { name: "Search highlights and notes" }).or(
    page.getByRole("textbox", { name: "Search highlights and notes" }),
  );
  await search.fill("metaphor");
  const result = page.getByRole("link", { name: new RegExp(PHRASE) });
  await expect(result).toBeVisible({ timeout: 15_000 });
  await expect(page.getByRole("region", { name: "Nitrate Annotation Fixture" })).toContainText(NOTE);
  expect(await result.getAttribute("href")).toContain(`/books/${bookId}/reader`);

  // 4. CSV export downloads the quote and the note.
  const [download] = await Promise.all([
    page.waitForEvent("download"),
    page.getByRole("link", { name: "CSV" }).click(),
  ]);
  expect(download.suggestedFilename()).toMatch(/^nitrate-annotations-\d{8}\.csv$/);
  const csv = await readFile((await download.path())!, "utf8");
  expect(csv).toContain(PHRASE);
  expect(csv).toContain(NOTE);

  await context.close();
});

test("an open, idle reader follows progress from another device instead of overwriting it", async ({
  browser,
  request,
  baseURL,
}) => {
  test.setTimeout(120_000);
  const deviceA = await browser.newContext();
  const token = await signInContext(deviceA, request, baseURL!);
  const bookId = await ensureAnnotationFixtureBook(request, token);
  await resetBookProgress(request, token, bookId);
  const pageA = await deviceA.newPage();
  await openReaderScrolled(pageA, bookId);

  const deviceB = await browser.newContext();
  await signInContext(deviceB, request, baseURL!);
  const pageB = await deviceB.newPage();
  await openReaderScrolled(pageB, bookId);
  const progressFromB = pageB.waitForRequest(
    (r) => r.url().includes(`/api/entities/${bookId}/progress`) && r.method() === "PATCH"
      && ((r.postDataJSON()?.location as string | undefined) ?? "").startsWith("epubcfi(/6/4!"),
  );
  await revealReaderChrome(pageB);
  await pageB.getByRole("button", { name: "Table of contents" }).click();
  await pageB.getByRole("button", { name: "The Tide" }).click();
  await progressFromB;

  // No focus or visibility event on A: its 15 s activity heartbeat must adopt B's position, not overwrite it.
  await expect(pageA.getByText("Moved to where you left off on another device.")).toBeVisible({ timeout: 30_000 });
  const auth = { Authorization: `Bearer ${token}` };
  const entity = await (await request.get(`/api/entities/${bookId}`, { headers: auth })).json();
  const progress = (entity.capabilities as Array<{ kind: string; location?: string }>).find((c) => c.kind === CAPABILITY_KIND.progress);
  expect(progress?.location ?? "").toMatch(/^epubcfi\(\/6\/4!/);

  await deviceA.close();
  await deviceB.close();
});

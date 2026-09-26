import { test, expect, type Page, type Response } from "@playwright/test";
import { signInContext } from "./auth";
import { ensureAnnotationFixtureBook, resetBookProgress } from "./book-fixture";
import { bookSectionFrame, selectInFrame, textCentre } from "./reader-helpers";

const PHRASE = "luminous harbor lanterns";

// Both tests share one account and one fixture book, so they must never run concurrently
// (run repeats with --workers=1).
test.describe.configure({ mode: "serial" });

async function openReader(page: Page, bookId: string) {
  const annotationsLoaded = page.waitForResponse(
    (r) => r.url().includes(`/api/books/${bookId}/annotations`) && r.request().method() === "GET",
  );
  await page.goto(`/books/${bookId}/reader`);
  const scrolled = page.getByRole("button", { name: "Scrolled reading" });
  await expect(scrolled).toBeVisible({ timeout: 30_000 });
  if ((await scrolled.getAttribute("aria-pressed")) !== "true") await scrolled.click();
  return (await (await annotationsLoaded).json()) as { annotations: Array<Record<string, unknown>> };
}

function isAnnotationWrite(r: Response, bookId: string, method: string) {
  return r.url().includes(`/api/books/${bookId}/annotations/`) && r.request().method() === method;
}

/** Simulates the tab regaining focus after the reader's 5 s refresh throttle. */
async function refocus(page: Page) {
  await page.waitForTimeout(5_500);
  await page.evaluate(() => window.dispatchEvent(new Event("focus")));
}

test("highlights persist across reloads and sync to another device; reading position follows", async ({
  browser,
  request,
  baseURL,
}) => {
  test.setTimeout(180_000);
  const deviceA = await browser.newContext();
  const token = await signInContext(deviceA, request, baseURL!);
  const bookId = await ensureAnnotationFixtureBook(request, token);
  await resetBookProgress(request, token, bookId);
  const pageA = await deviceA.newPage();

  // Start clean: tombstone any live highlights left by earlier runs, before the reader loads them.
  const auth = { Authorization: `Bearer ${token}` };
  const leftovers = (await (await request.get(`/api/books/${bookId}/annotations`, { headers: auth })).json()) as {
    annotations: Array<{ id: string; deletedAt: string | null }>;
  };
  for (const existing of leftovers.annotations.filter((a) => !a.deletedAt)) {
    const removed = await request.delete(
      `/api/books/${bookId}/annotations/${existing.id}?updatedAt=${encodeURIComponent(new Date().toISOString())}`,
      { headers: auth },
    );
    expect((await removed.json()).deletedAt, "cleanup delete was not applied").not.toBeNull();
  }

  // 1. Highlight on device A.
  await openReader(pageA, bookId);
  const frameA = await bookSectionFrame(pageA, PHRASE);
  await selectInFrame(frameA, PHRASE);
  const saved = pageA.waitForResponse((r) => isAnnotationWrite(r, bookId, "PUT"));
  await pageA.getByRole("button", { name: "Highlight" }).click();
  const created = await (await saved).json();
  expect(created.selectedText).toBe(PHRASE);

  // 2. Reload: still there.
  const afterReload = await openReader(pageA, bookId);
  const live = afterReload.annotations.filter((a) => !a.deletedAt);
  expect(live.map((a) => a.id)).toEqual([created.id]);
  expect(live[0]?.selectedText).toBe(PHRASE);

  // 3. Device B (same user) sees it, and changes its colour by tapping the highlight.
  const deviceB = await browser.newContext();
  await signInContext(deviceB, request, baseURL!);
  const pageB = await deviceB.newPage();
  const onB = await openReader(pageB, bookId);
  expect(onB.annotations.filter((a) => !a.deletedAt).map((a) => a.id)).toEqual([created.id]);
  const frameB = await bookSectionFrame(pageB, PHRASE);
  await expect
    .poll(async () => {
      const centre = await textCentre(frameB, PHRASE);
      await pageB.mouse.click(centre.x, centre.y);
      return pageB.getByRole("button", { name: "Blue" }).isVisible();
    }, { timeout: 20_000 })
    .toBe(true);
  const recoloured = pageB.waitForResponse((r) => isAnnotationWrite(r, bookId, "PUT"));
  await pageB.getByRole("button", { name: "Blue" }).click();
  expect((await (await recoloured).json()).color).toBe("blue");

  // 4. Device A picks the change up when it regains focus.
  const refreshedOnA = pageA.waitForResponse(
    (r) => r.url().includes(`/api/books/${bookId}/annotations`) && r.request().method() === "GET",
  );
  await refocus(pageA);
  const refreshed = await (await refreshedOnA).json();
  expect(refreshed.annotations.find((a: { id: string }) => a.id === created.id)?.color).toBe("blue");

  // 5. Reading position follows: B moves on, A catches up on focus and offers "Go back".
  await pageB.keyboard.press("Escape");
  const progressFromB = pageB.waitForRequest(
    (r) => r.url().includes(`/api/entities/${bookId}/progress`) && r.method() === "PATCH",
  );
  // Reveal the auto-hidden chrome the way a mouse does: move gradually out of the book iframe into
  // the top hover zone (a single jump out of an iframe does not fire pointerenter in headless Chromium).
  await pageB.mouse.move(700, 600);
  await pageB.mouse.move(700, 30, { steps: 8 });
  await pageB.getByRole("button", { name: "Table of contents" }).click();
  await pageB.getByRole("button", { name: "The Tide" }).click();
  const bLocation = (await progressFromB).postDataJSON().location as string;
  expect(bLocation).toMatch(/^epubcfi\(/);

  const followedOnA = pageA.waitForRequest(
    (r) => r.url().includes(`/api/entities/${bookId}/progress`) && r.method() === "PATCH"
      && ((r.postDataJSON()?.location as string | undefined) ?? "").startsWith(bLocation.slice(0, 12)),
  );
  await refocus(pageA);
  await expect(pageA.getByText("Moved to where you left off on another device.")).toBeVisible({ timeout: 15_000 });
  await followedOnA;
  await expect(pageA.getByRole("button", { name: "Go back" })).toBeVisible();

  await deviceA.close();
  await deviceB.close();
});

test("paged mode: a real mouse drag in the middle of the page selects text and opens the menu", async ({
  browser,
  request,
  baseURL,
}) => {
  test.setTimeout(120_000);
  const context = await browser.newContext();
  const token = await signInContext(context, request, baseURL!);
  const bookId = await ensureAnnotationFixtureBook(request, token);
  await resetBookProgress(request, token, bookId);
  const page = await context.newPage();
  await page.goto(`/books/${bookId}/reader`);
  const paged = page.getByRole("button", { name: "Paged reading" });
  await expect(paged).toBeVisible({ timeout: 30_000 });
  if ((await paged.getAttribute("aria-pressed")) !== "true") await paged.click();

  // Drag horizontally from the centre of the page across the first paragraph's first line: the middle
  // of the page must reach the book (only the 18% edge strips turn pages), so text gets selected.
  await page.waitForTimeout(1_500); // let foliate finish re-laying out the columns after the flow switch
  const frame = await bookSectionFrame(page, "The harbour woke slowly");
  const line = await frame.evaluate(() => {
    const r = document.querySelector("p")!.getClientRects()[0]!;
    const f = window.frameElement?.getBoundingClientRect() ?? { left: 0, top: 0 };
    return {
      x1: f.left + r.left + r.width * 0.3,
      x2: f.left + r.left + r.width * 0.6,
      y: f.top + r.top + r.height / 2,
    };
  });
  const viewport = page.viewportSize()!;
  // Starts outside the 18% page-turn strip, so it proves the book (not the tap layer) gets the pointer.
  expect(line.x1).toBeGreaterThan(viewport.width * 0.18);
  // Whole-pixel coordinates: synthetic drags at fractional positions into the section iframe do not
  // extend the selection in headless Chromium (a test-input quirk; real-device selection is owner-verified).
  const [x1, x2, y] = [Math.round(line.x1), Math.round(line.x2), Math.round(line.y)];
  await page.mouse.move(x1, y);
  await page.mouse.down();
  await page.mouse.move(x2, y, { steps: 10 });
  await page.mouse.up();

  await expect(page.getByRole("button", { name: "Highlight" })).toBeVisible({ timeout: 10_000 });
  await expect(page.getByRole("button", { name: "Add Note" })).toBeVisible();
  const selected = await frame.evaluate(() => document.getSelection()?.toString() ?? "");
  expect(selected.trim().length).toBeGreaterThan(3);
  await context.close();
});

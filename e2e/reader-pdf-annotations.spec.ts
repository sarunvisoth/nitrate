import { test, expect, type APIRequestContext, type Page } from "@playwright/test";
import { signInContext } from "./auth";
import { ensureAnnotationFixturePdfBook, resetPdfBookProgress } from "./book-fixture";
import { selectOnPage } from "./reader-helpers";

// One account + one fixture book shared across assertions; run with --workers=1.
test.describe.configure({ mode: "serial" });

const PAGE_1_PHRASE = "harbour woke slowly under a pale sky";
const PAGE_3_ONLY_WORD = "paperclip";

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

async function openReader(page: Page, bookId: string) {
  const annotationsLoaded = page.waitForResponse(
    (r) => r.url().includes(`/api/books/${bookId}/annotations`) && r.request().method() === "GET",
  );
  await page.goto(`/books/${bookId}/reader`);
  await expect(page.getByRole("button", { name: "Search document" })).toBeVisible({ timeout: 30_000 });
  await annotationsLoaded;
}

test("select text on a page creates a highlight that survives reload on the right page", async ({
  browser,
  request,
  baseURL,
}) => {
  test.setTimeout(120_000);
  const deviceA = await browser.newContext();
  const token = await signInContext(deviceA, request, baseURL!);
  const bookId = await ensureAnnotationFixturePdfBook(request, token);
  await resetPdfBookProgress(request, token, bookId);
  await clearLeftovers(request, token, bookId);
  const page = await deviceA.newPage();

  await openReader(page, bookId);
  const centre = await selectOnPage(page, PAGE_1_PHRASE);
  await page.mouse.move(centre.x, centre.y);

  const saved = page.waitForResponse(
    (r) => r.url().includes(`/api/books/${bookId}/annotations/`) && r.request().method() === "PUT",
  );
  await expect(page.getByRole("button", { name: "Highlight" })).toBeVisible({ timeout: 5_000 });
  await page.getByRole("button", { name: "Highlight" }).click();
  const created = await (await saved).json();
  expect(created.selectedText).toContain(PAGE_1_PHRASE);
  expect(created.locatorKind).toBe("pdf");
  expect(JSON.parse(created.locator).page).toBe(0);

  // Reload: the highlight is still there, on the same page, drawn as an overlay rect.
  const afterReload = await new Promise<{ annotations: Array<{ id: string; deletedAt: string | null }> }>(
    (resolve) => {
      page.on("response", async (response) => {
        if (response.url().includes(`/api/books/${bookId}/annotations`) && response.request().method() === "GET") {
          resolve(await response.json());
        }
      });
      void page.reload();
    },
  );
  const live = afterReload.annotations.filter((a) => !a.deletedAt);
  expect(live.map((a) => a.id)).toEqual([created.id]);
  await expect(page.locator(".pdf-highlight-rect")).toHaveCount(1, { timeout: 15_000 });
});

test("bookmarking a page adds it to the contents sheet's Bookmarks tab", async ({ browser, request, baseURL }) => {
  test.setTimeout(90_000);
  const context = await browser.newContext();
  const token = await signInContext(context, request, baseURL!);
  const bookId = await ensureAnnotationFixturePdfBook(request, token);
  await resetPdfBookProgress(request, token, bookId);
  await clearLeftovers(request, token, bookId);
  const page = await context.newPage();

  await openReader(page, bookId);
  const bookmarkButton = page.getByRole("button", { name: "Bookmark this page" });
  await expect(bookmarkButton).toBeVisible({ timeout: 15_000 });
  const saved = page.waitForResponse(
    (r) => r.url().includes(`/api/books/${bookId}/bookmarks/`) && r.request().method() === "PUT",
  );
  await bookmarkButton.click();
  await saved;
  await expect(page.getByRole("button", { name: "Remove bookmark" })).toBeVisible();

  await page.getByRole("button", { name: "Table of contents" }).click();
  await page.getByRole("tab", { name: "Bookmarks (1)" }).click();
  await expect(page.getByRole("button", { name: /Bookmark/ })).toBeVisible();
});

test("search finds text on a later page and navigates to it", async ({ browser, request, baseURL }) => {
  test.setTimeout(90_000);
  const context = await browser.newContext();
  const token = await signInContext(context, request, baseURL!);
  const bookId = await ensureAnnotationFixturePdfBook(request, token);
  await resetPdfBookProgress(request, token, bookId);
  const page = await context.newPage();

  await openReader(page, bookId);
  await page.getByRole("button", { name: "Search document" }).click();
  await page.getByPlaceholder("Search this document").fill(PAGE_3_ONLY_WORD);
  await page.keyboard.press("Enter");

  const result = page.getByText(new RegExp(PAGE_3_ONLY_WORD, "i")).first();
  await expect(result).toBeVisible({ timeout: 15_000 });
  await expect(page.getByText("p. 3")).toBeVisible();
  await result.click();

  await expect(page.locator("body")).toContainText(PAGE_3_ONLY_WORD, { timeout: 15_000 });
});

test("the highlight appears on the annotations dashboard with its page number", async ({
  browser,
  request,
  baseURL,
}) => {
  test.setTimeout(90_000);
  const context = await browser.newContext();
  const token = await signInContext(context, request, baseURL!);
  const bookId = await ensureAnnotationFixturePdfBook(request, token);
  await resetPdfBookProgress(request, token, bookId);
  await clearLeftovers(request, token, bookId);
  const page = await context.newPage();

  await openReader(page, bookId);
  const centre = await selectOnPage(page, PAGE_1_PHRASE);
  await page.mouse.move(centre.x, centre.y);
  const saved = page.waitForResponse(
    (r) => r.url().includes(`/api/books/${bookId}/annotations/`) && r.request().method() === "PUT",
  );
  await expect(page.getByRole("button", { name: "Highlight" })).toBeVisible({ timeout: 5_000 });
  await page.getByRole("button", { name: "Highlight" }).click();
  await saved;

  await page.goto("/annotations");
  await expect(page.getByText(PAGE_1_PHRASE)).toBeVisible({ timeout: 15_000 });
  await expect(page.getByText("p. 1")).toBeVisible();
});

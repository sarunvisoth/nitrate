import { test, expect, type Page } from "@playwright/test";
import { signInContext } from "./auth";
import { ANNOTATION_FIXTURE_TITLE, ensureAnnotationFixtureBook, resetBookProgress } from "./book-fixture";
import { bookSectionFrame, selectInFrame } from "./reader-helpers";

// Shares the fixture book/account with the other reader specs; run with --workers=1.
test.describe.configure({ mode: "serial" });

async function mockDefine(page: Page) {
  await page.route("**/api/reader/define*", async (route) => {
    await route.fulfill({
      json: {
        term: "luminous",
        language: "en",
        entries: [
          {
            partOfSpeech: "adjective",
            definitions: [{ text: "Giving off light; bright.", examples: ["a luminous glow"] }],
          },
        ],
        sourceUrl: "https://en.wiktionary.org/wiki/luminous",
        provider: "wiktionary",
        attribution: "Wiktionary, CC BY-SA",
      },
    });
  });
}

async function openReader(page: Page, bookId: string) {
  await page.goto(`/books/${bookId}/reader`);
  const scrolled = page.getByRole("button", { name: "Scrolled reading" });
  await expect(scrolled).toBeVisible({ timeout: 30_000 });
  if ((await scrolled.getAttribute("aria-pressed")) !== "true") await scrolled.click();
}

test("Changing theme and font size in reader settings persists after reload", async ({ browser, request, baseURL }) => {
  test.setTimeout(120_000);
  const context = await browser.newContext();
  const token = await signInContext(context, request, baseURL!);
  const bookId = await ensureAnnotationFixtureBook(request, token);
  await resetBookProgress(request, token, bookId);
  const page = await context.newPage();

  await openReader(page, bookId);

  await page.getByRole("button", { name: "Reading settings" }).click();
  const themeSelect = page.getByRole("button", { name: "Reader theme" });
  await expect(themeSelect).toBeVisible();
  await themeSelect.click();
  await page.getByRole("option", { name: "Sepia" }).click();

  const fontSizeSlider = page.getByRole("slider", { name: "Font size" });
  await expect(fontSizeSlider).toBeVisible();
  for (let i = 0; i < 6; i++) await fontSizeSlider.press("ArrowRight"); // +5% per step, +30%

  // Debounced save (~500ms); wait for it to reach the server before reloading.
  await expect
    .poll(async () => {
      const response = await request.get(`${baseURL}/api/reader/preferences`, {
        headers: { Cookie: `prismedia-session=${token}` },
      });
      if (!response.ok()) return null;
      const body = await response.json();
      return `${body.theme}:${body.fontSizePercent}`;
    }, { timeout: 10_000 })
    .toBe("sepia:130");

  await page.reload();
  await expect(page.getByRole("button", { name: "Scrolled reading" })).toBeVisible({ timeout: 30_000 });
  await page.getByRole("button", { name: "Reading settings" }).click();
  await expect(page.getByRole("button", { name: "Reader theme" })).toHaveText("Sepia");

  await context.close();
});

test("Define, Add to Vocabulary, then change status on /vocabulary", async ({ browser, request, baseURL }) => {
  test.setTimeout(120_000);
  const context = await browser.newContext();
  const token = await signInContext(context, request, baseURL!);
  const bookId = await ensureAnnotationFixtureBook(request, token);
  await resetBookProgress(request, token, bookId);
  const page = await context.newPage();
  await mockDefine(page);

  await openReader(page, bookId);
  const frame = await bookSectionFrame(page, "luminous");
  const point = await selectInFrame(frame, "luminous");
  await page.mouse.move(point.x, point.y);

  const defineButton = page.getByRole("button", { name: "Define" });
  await expect(defineButton).toBeVisible({ timeout: 10_000 });
  await defineButton.click();
  await expect(page.getByText("Giving off light; bright.")).toBeVisible();

  await page.getByRole("button", { name: "Add to Vocabulary" }).click();
  await expect(page.getByRole("button", { name: "In vocabulary" })).toBeVisible();

  await page.goto("/vocabulary");
  // The saved word links back to its source book + location (bookId/CFI locator round-tripped),
  // shows the book title, and the surrounding sentence captured from the text-quote before/after.
  const wordLink = page.getByRole("link", { name: "luminous" });
  await expect(wordLink).toBeVisible({ timeout: 15_000 });
  await expect(page.getByText(ANNOTATION_FIXTURE_TITLE)).toBeVisible();
  await expect(page.locator("p", { hasText: "luminous" }).filter({ hasText: "“" })).toBeVisible();

  const statusSelect = page.getByRole("button", { name: "Status for luminous" });
  await statusSelect.click();
  await page.getByRole("option", { name: "Known" }).click();
  await expect(statusSelect).toHaveText("Known");

  await page.reload();
  await expect(page.getByRole("button", { name: "Status for luminous" })).toHaveText("Known", { timeout: 15_000 });

  // Jump to source: the word link opens the reader on the saved book.
  await page.getByRole("link", { name: "luminous" }).click();
  await expect(page).toHaveURL(new RegExp(`/books/${bookId}/reader`));

  await context.close();
});

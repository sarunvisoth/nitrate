import { test, expect, type Page } from "@playwright/test";
import { signInContext } from "./auth";
import { ensureAnnotationFixtureBook, resetBookProgress } from "./book-fixture";
import { bookSectionFrame, selectInFrame } from "./reader-helpers";

// Shares the fixture book with reader-annotations.spec.ts; run with --workers=1.
test.describe.configure({ mode: "serial" });

async function mockReaderLookups(page: Page) {
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

  await page.route("**/api/reader/translate*", async (route) => {
    const body = route.request().postDataJSON() as { text: string; targetLanguage: string };
    await route.fulfill({
      json: {
        text: body.text,
        translatedText: "lumineux",
        sourceLanguage: "en",
        targetLanguage: body.targetLanguage,
        provider: "mymemory",
      },
    });
  });

  await page.route("**/api/reader/wikipedia*", async (route) => {
    await route.fulfill({
      json: {
        title: "Luminous",
        extract: "Luminous means emitting or reflecting light.",
        url: "https://en.wikipedia.org/wiki/Luminous",
        thumbnailUrl: null,
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

test("Define and Translate use the mocked /api/reader endpoints", async ({ browser, request, baseURL }) => {
  test.setTimeout(120_000);
  const context = await browser.newContext();
  const token = await signInContext(context, request, baseURL!);
  const bookId = await ensureAnnotationFixtureBook(request, token);
  await resetBookProgress(request, token, bookId);
  const page = await context.newPage();
  await mockReaderLookups(page);

  await openReader(page, bookId);
  const frame = await bookSectionFrame(page, "luminous");
  const point = await selectInFrame(frame, "luminous");
  await page.mouse.move(point.x, point.y);

  const defineButton = page.getByRole("button", { name: "Define" });
  await expect(defineButton).toBeVisible({ timeout: 10_000 });
  await defineButton.click();

  await expect(page.getByText("Giving off light; bright.")).toBeVisible();
  await expect(page.getByText("Wiktionary, CC BY-SA")).toBeVisible();

  await page.getByRole("button", { name: "Translate" }).click();
  await expect(page.getByText("lumineux")).toBeVisible();

  await page.getByRole("button", { name: "Wikipedia" }).click();
  await expect(page.getByText("Luminous means emitting or reflecting light.")).toBeVisible();

  await context.close();
});

test("Search book finds a match and navigates to it", async ({ browser, request, baseURL }) => {
  test.setTimeout(120_000);
  const context = await browser.newContext();
  const token = await signInContext(context, request, baseURL!);
  const bookId = await ensureAnnotationFixtureBook(request, token);
  await resetBookProgress(request, token, bookId);
  const page = await context.newPage();
  await mockReaderLookups(page);

  await openReader(page, bookId);
  await page.getByRole("button", { name: "Search book" }).click();

  const searchInput = page.getByRole("searchbox", { name: "Search this book" });
  await expect(searchInput).toBeVisible();
  await searchInput.fill("luminous");
  await page.getByRole("button", { name: "Search", exact: true }).click();

  const result = page.locator("mark", { hasText: "luminous" }).first();
  await expect(result).toBeVisible({ timeout: 15_000 });
  await result.click();

  // The search sheet closes after navigating to a result.
  await expect(searchInput).not.toBeVisible();

  await context.close();
});

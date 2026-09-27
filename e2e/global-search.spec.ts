import { test, expect } from "@playwright/test";
import { loginAsE2eUser } from "./auth";
import { ensureAnnotationFixtureBook } from "./book-fixture";
import { createUuid } from "../apps/web-svelte/src/lib/utils/uuid";

const API_BASE = process.env.PRISMEDIA_E2E_API_URL ?? "http://127.0.0.1:8008/api";
const SESSION_COOKIE = "prismedia-session";

// Shares one account and fixture book with the other reader specs; keep serial under --workers=1.
test.describe.configure({ mode: "serial" });

test.describe("unified global search", () => {
  test("finds a highlight from /search and from the command palette", async ({ page, request, baseURL }) => {
    test.setTimeout(120_000);
    const token = await loginAsE2eUser(request);
    const url = new URL(baseURL!);
    await page.context().addCookies([
      { name: SESSION_COOKIE, value: token, domain: url.hostname, path: "/", httpOnly: true, sameSite: "Lax" },
    ]);
    const headers = { Authorization: `Bearer ${token}` };
    const bookId = await ensureAnnotationFixtureBook(request, token);

    const phrase = `nitrate-search-marker-${Date.now()}`;
    const annotationId = createUuid();
    const created = await request.put(`${API_BASE}/books/${bookId}/annotations/${annotationId}`, {
      headers,
      data: {
        locatorKind: "epub-cfi",
        locator: "/6/4",
        spineIndex: 0,
        position: 0.1,
        selectedText: phrase,
        textBefore: "",
        textAfter: "",
        style: "highlight",
        color: "yellow",
        updatedAt: new Date().toISOString(),
      },
    });
    expect(created.ok(), await created.text()).toBeTruthy();

    const word = `nitrate-vocab-marker-${Date.now()}`;
    const vocabularyId = createUuid();
    const savedVocabulary = await request.put(`${API_BASE}/vocabulary/${vocabularyId}`, {
      headers,
      data: {
        word,
        language: "en",
        definition: "a marker definition for the global search e2e test",
        bookId: null,
        sourceLocator: null,
        sourceSentence: null,
        status: "new",
        updatedAt: new Date().toISOString(),
      },
    });
    expect(savedVocabulary.ok(), await savedVocabulary.text()).toBeTruthy();

    // /search finds both.
    await page.goto(`/search?q=${encodeURIComponent(phrase)}`);
    await expect(page.getByRole("heading", { name: "Annotations" })).toBeVisible();
    await expect(page.getByText(phrase)).toBeVisible();

    await page.goto(`/search?q=${encodeURIComponent(word)}`);
    await expect(page.getByRole("heading", { name: "Vocabulary" })).toBeVisible();
    await expect(page.getByText(word, { exact: true })).toBeVisible();

    // Command palette finds the annotation and links to the reader at that location.
    await page.goto("/");
    await page.getByRole("button", { name: "Open search" }).click();
    await page.getByRole("dialog").getByLabel("Search library").fill(phrase);
    const annotationItem = page.getByRole("option").filter({ hasText: phrase }).filter({ hasNotText: "See all results" });
    await expect(annotationItem).toBeVisible();
    await annotationItem.click();
    await expect(page).toHaveURL(new RegExp(`/books/${bookId}/reader`));

    // Command palette finds the vocabulary word and its "see all" link goes to /vocabulary?q=.
    await page.goto("/");
    await page.getByRole("button", { name: "Open search" }).click();
    await page.getByRole("dialog").getByLabel("Search library").fill(word);
    const vocabularyItem = page.getByRole("option").filter({ hasText: word }).filter({ hasNotText: "See all results" });
    await expect(vocabularyItem).toBeVisible();
    await page.getByRole("option", { name: /See all vocabulary/i }).click();
    await expect(page).toHaveURL(new RegExp(`/vocabulary\\?q=${word}`));
    await expect(page.getByText(word, { exact: true })).toBeVisible();
  });
});

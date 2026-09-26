import path from "node:path";
import { expect, type APIRequestContext } from "@playwright/test";
import { PROGRESS_UNIT } from "../apps/web-svelte/src/lib/api/generated/codes";

const API_BASE = process.env.PRISMEDIA_E2E_API_URL ?? "http://127.0.0.1:8008/api";
export const ANNOTATION_FIXTURE_TITLE = "Nitrate Annotation Fixture";
const FIXTURE_DIR = path.resolve(process.cwd(), "tests/fixtures/media/books");

type LibraryRoot = { id: string; path: string };
type EntityList = { items: Array<{ id: string; title: string }> };

/** Ensures a Books root on the committed fixture folder, waits for the scan, and returns the Book id. */
export async function ensureAnnotationFixtureBook(request: APIRequestContext, token: string): Promise<string> {
  const headers = { Authorization: `Bearer ${token}` };
  const roots = (await (await request.get(`${API_BASE}/libraries`, { headers })).json()) as LibraryRoot[];
  if (!roots.some((root) => root.path === FIXTURE_DIR)) {
    const created = await request.post(`${API_BASE}/libraries`, {
      headers,
      data: {
        path: FIXTURE_DIR,
        label: "E2E Books",
        enabled: true,
        recursive: true,
        scanVideos: false,
        scanImages: false,
        scanAudio: false,
        scanBooks: true,
      },
    });
    expect(created.ok(), await created.text()).toBeTruthy();
  }

  const deadline = Date.now() + 90_000;
  while (Date.now() < deadline) {
    const response = await request.get(`${API_BASE}/books?limit=50`, { headers });
    if (response.ok()) {
      const book = ((await response.json()) as EntityList).items.find(
        (item) => item.title === ANNOTATION_FIXTURE_TITLE,
      );
      if (book) return book.id;
    }
    await new Promise((resolve) => setTimeout(resolve, 1_000));
  }
  throw new Error("Timed out waiting for the annotation fixture book to be scanned");
}

/** Resets the user's reading progress on the book so every run starts on the first page. */
export async function resetBookProgress(request: APIRequestContext, token: string, bookId: string) {
  const response = await request.patch(`${API_BASE}/entities/${bookId}/progress`, {
    headers: { Authorization: `Bearer ${token}`, Prefer: "return=minimal" },
    data: { currentEntityId: bookId, unit: PROGRESS_UNIT.cfi, index: 0, total: 10_000, reset: true },
  });
  expect(response.ok(), await response.text()).toBeTruthy();
}

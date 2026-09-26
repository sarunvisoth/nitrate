import { test, expect, type Page } from "@playwright/test";
import { loginAsE2eUser } from "./auth";
import { ensureAnnotationFixtureBook } from "./book-fixture";
import { bookSectionFrame, selectInFrame } from "./reader-helpers";

const SESSION_COOKIE = "prismedia-session";
const PHRASE = "luminous harbor lanterns";

test.describe.configure({ mode: "serial" });

test.use({
  viewport: { width: 375, height: 812 },
  hasTouch: true,
  isMobile: true,
});

/** No horizontal scrollbar: the layout fits the viewport width exactly. */
async function expectNoHorizontalOverflow(page: Page) {
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - window.innerWidth,
  );
  expect(overflow, "page has horizontal overflow at phone width").toBeLessThanOrEqual(0);
}

/** Every visible dialog/popover surface stays within the viewport, with a small margin of error. */
async function expectPopoversOnScreen(page: Page) {
  const boxes = await page.evaluate(() => {
    const nodes = document.querySelectorAll<HTMLElement>('[role="dialog"], [data-slot="popover-content"]');
    return Array.from(nodes).map((node) => {
      const rect = node.getBoundingClientRect();
      return { left: rect.left, top: rect.top, right: rect.right, bottom: rect.bottom };
    });
  });
  for (const box of boxes) {
    expect(box.left).toBeGreaterThanOrEqual(-1);
    expect(box.top).toBeGreaterThanOrEqual(-1);
    expect(box.right).toBeLessThanOrEqual((await page.viewportSize())!.width + 1);
    expect(box.bottom).toBeLessThanOrEqual((await page.viewportSize())!.height + 1);
  }
}

test.describe("mobile smoke", () => {
  test("core reader and library-search surfaces fit a phone viewport with touch input", async ({
    page,
    request,
    baseURL,
  }) => {
    test.setTimeout(120_000);
    const token = await loginAsE2eUser(request);
    const url = new URL(baseURL!);
    await page.context().addCookies([
      { name: SESSION_COOKIE, value: token, domain: url.hostname, path: "/", httpOnly: true, sameSite: "Lax" },
    ]);
    const bookId = await ensureAnnotationFixtureBook(request, token);

    // Library-search surfaces.
    await page.goto("/search?q=dune");
    await expectNoHorizontalOverflow(page);

    await page.goto("/annotations");
    await expectNoHorizontalOverflow(page);

    await page.goto("/vocabulary");
    await expectNoHorizontalOverflow(page);

    // Command palette opens as a full-width sheet-like dialog on a phone and stays on-screen.
    await page.goto("/");
    await expectNoHorizontalOverflow(page);
    await page.getByRole("button", { name: "Open search" }).click();
    await expect(page.getByRole("dialog")).toBeVisible();
    await expectPopoversOnScreen(page);
    await page.keyboard.press("Escape");

    // Reader: selection popover (annotation toolbar) must clamp within the viewport.
    await page.goto(`/books/${bookId}/reader`);
    const scrolled = page.getByRole("button", { name: "Scrolled reading" });
    await expect(scrolled).toBeVisible({ timeout: 30_000 });
    await expectNoHorizontalOverflow(page);

    const frame = await bookSectionFrame(page, PHRASE);
    const centre = await selectInFrame(frame, PHRASE);
    await page.mouse.move(centre.x, centre.y);
    await page.mouse.up();
    // The selection toolbar/menu (a Popover.Content) should appear and clamp on-screen.
    await expect
      .poll(async () => page.locator('[data-slot="popover-content"]').count())
      .toBeGreaterThan(0);
    await expectPopoversOnScreen(page);
    await expectNoHorizontalOverflow(page);
  });
});

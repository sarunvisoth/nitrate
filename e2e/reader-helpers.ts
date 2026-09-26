import { expect, type Frame, type Page } from "@playwright/test";

export function bookSectionFrame(page: Page, text: string): Promise<Frame> {
  // foliate renders sections in iframes inside a closed shadow root; page.frames() still lists them.
  return expect
    .poll(async () => {
      for (const frame of page.frames()) {
        const found = await frame.evaluate((t) => document.body?.textContent?.includes(t) ?? false, text).catch(() => false);
        if (found) return frame;
      }
      return null;
    }, { timeout: 30_000 })
    .not.toBeNull()
    .then(async () => {
      for (const frame of page.frames()) {
        if (await frame.evaluate((t) => document.body?.textContent?.includes(t) ?? false, text).catch(() => false)) {
          return frame;
        }
      }
      throw new Error(`No book frame contains "${text}"`);
    });
}

/** Selects the first occurrence of `text` inside a section frame and returns its top-window centre. */
export async function selectInFrame(frame: Frame, text: string) {
  return frame.evaluate((t) => {
    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
    let node: Node | null;
    while ((node = walker.nextNode())) {
      const at = node.textContent?.indexOf(t) ?? -1;
      if (at < 0) continue;
      const range = document.createRange();
      range.setStart(node, at);
      range.setEnd(node, at + t.length);
      range.startContainer.parentElement?.scrollIntoView({ block: "center" });
      const selection = document.getSelection()!;
      selection.removeAllRanges();
      selection.addRange(range);
      document.dispatchEvent(new PointerEvent("pointerup", { bubbles: true }));
      const r = range.getBoundingClientRect();
      const f = window.frameElement?.getBoundingClientRect() ?? { left: 0, top: 0 };
      return { x: f.left + r.left + r.width / 2, y: f.top + r.top + r.height / 2 };
    }
    throw new Error(`text not found: ${t}`);
  }, text);
}

/** Top-window centre of `text` inside a section frame (without changing the selection). */
export async function textCentre(frame: Frame, text: string) {
  return frame.evaluate((t) => {
    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
    let node: Node | null;
    while ((node = walker.nextNode())) {
      const at = node.textContent?.indexOf(t) ?? -1;
      if (at < 0) continue;
      const range = document.createRange();
      range.setStart(node, at);
      range.setEnd(node, at + t.length);
      const r = range.getBoundingClientRect();
      const f = window.frameElement?.getBoundingClientRect() ?? { left: 0, top: 0 };
      return { x: f.left + r.left + r.width / 2, y: f.top + r.top + r.height / 2 };
    }
    throw new Error(`text not found: ${t}`);
  }, text);
}


/**
 * Selects the first occurrence of `text` directly on the page (PdfReader's text layer lives in the
 * main document, unlike foliate's per-section iframes) and returns its viewport centre.
 */
export async function selectOnPage(page: Page, text: string) {
  return page.evaluate((t) => {
    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
    let node: Node | null;
    while ((node = walker.nextNode())) {
      const at = node.textContent?.indexOf(t) ?? -1;
      if (at < 0) continue;
      const range = document.createRange();
      range.setStart(node, at);
      range.setEnd(node, at + t.length);
      range.startContainer.parentElement?.scrollIntoView({ block: "center" });
      const selection = document.getSelection()!;
      selection.removeAllRanges();
      selection.addRange(range);
      document.dispatchEvent(new Event("selectionchange", { bubbles: true }));
      const r = range.getBoundingClientRect();
      return { x: r.left + r.width / 2, y: r.top + r.height / 2 };
    }
    throw new Error(`text not found: ${t}`);
  }, text);
}

/** Reveals the reader's auto-hidden top bar the way a mouse does (gradual move out of the book iframe). */
export async function revealReaderChrome(page: Page) {
  await page.mouse.move(700, 600);
  await page.mouse.move(700, 30, { steps: 8 });
}

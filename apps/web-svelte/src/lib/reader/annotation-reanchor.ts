import type { TextQuote } from "./epub-selection";

const normalize = (value: string) => value.replace(/\s+/g, " ").trim();

/** True when two highlighted strings are the same text, ignoring whitespace differences. */
export function sameQuotedText(a: string, b: string): boolean {
  return normalize(a) === normalize(b);
}

interface TextSpan {
  node: Text;
  start: number;
}

/**
 * Re-finds a highlight's text in a (possibly changed) section document by its text quote. When the text occurs
 * more than once, prefers the occurrence whose surrounding text best matches the stored `textBefore`/`textAfter`.
 * Returns a Range over the match, or null when the text no longer exists.
 */
export function findTextQuote(doc: Document, quote: TextQuote): Range | null {
  const exact = quote.selectedText;
  if (!exact) return null;
  const root = doc.body ?? doc.documentElement;
  const walker = doc.createTreeWalker(root, 4 /* NodeFilter.SHOW_TEXT */);
  const spans: TextSpan[] = [];
  let text = "";
  for (let node = walker.nextNode(); node; node = walker.nextNode()) {
    spans.push({ node: node as Text, start: text.length });
    text += node.textContent ?? "";
  }

  let best: { at: number; score: number } | null = null;
  for (let at = text.indexOf(exact); at >= 0; at = text.indexOf(exact, at + 1)) {
    const score =
      commonSuffix(text.slice(Math.max(0, at - 256), at), quote.textBefore ?? "") +
      commonPrefix(text.slice(at + exact.length, at + exact.length + 256), quote.textAfter ?? "");
    if (!best || score > best.score) best = { at, score };
  }
  if (!best) return null;

  const start = locate(spans, best.at);
  const end = locate(spans, best.at + exact.length);
  if (!start || !end) return null;
  const range = doc.createRange();
  range.setStart(start.node, start.offset);
  range.setEnd(end.node, end.offset);
  return range;
}

function locate(spans: TextSpan[], offset: number): { node: Text; offset: number } | null {
  for (let i = spans.length - 1; i >= 0; i--) {
    const span = spans[i]!;
    if (span.start <= offset) {
      return { node: span.node, offset: Math.min(offset - span.start, span.node.length) };
    }
  }
  return null;
}

function commonSuffix(a: string, b: string): number {
  let n = 0;
  while (n < a.length && n < b.length && a[a.length - 1 - n] === b[b.length - 1 - n]) n++;
  return n;
}

function commonPrefix(a: string, b: string): number {
  let n = 0;
  while (n < a.length && n < b.length && a[n] === b[n]) n++;
  return n;
}

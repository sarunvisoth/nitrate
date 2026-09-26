/** Default characters of surrounding text stored on each side of a highlight for re-anchoring. */
export const TEXT_QUOTE_CONTEXT_LENGTH = 64;

/** Text-quote selector (exact text plus bounded prefix/suffix) for a range inside one section document. */
export interface TextQuote {
  selectedText: string;
  textBefore: string | null;
  textAfter: string | null;
}

/** Captures the exact selected text plus up to `contextLength` characters on each side, within the same document. */
export function textQuoteFor(range: Range, contextLength = TEXT_QUOTE_CONTEXT_LENGTH): TextQuote {
  const doc = range.startContainer.ownerDocument ?? document;
  const root = doc.body ?? doc.documentElement;
  const before = doc.createRange();
  before.setStart(root, 0);
  before.setEnd(range.startContainer, range.startOffset);
  const after = doc.createRange();
  after.setStart(range.endContainer, range.endOffset);
  after.setEnd(root, root.childNodes.length);
  return {
    selectedText: range.toString(),
    textBefore: before.toString().slice(-contextLength) || null,
    textAfter: after.toString().slice(0, contextLength) || null,
  };
}

interface RectLike {
  left: number;
  top: number;
  width: number;
  height: number;
}

interface PointLike {
  left: number;
  top: number;
}

/**
 * Converts a rect measured inside a section iframe into top-window coordinates, optionally relative
 * to a container (the reader stage) so a menu can be absolutely positioned inside it.
 */
export function selectionAnchorRect(
  local: RectLike,
  frame: PointLike | null,
  container: PointLike | null = null,
): RectLike {
  return {
    left: local.left + (frame?.left ?? 0) - (container?.left ?? 0),
    top: local.top + (frame?.top ?? 0) - (container?.top ?? 0),
    width: local.width,
    height: local.height,
  };
}

/** Wraps a viewport rect as a floating-ui "virtual element" so a Popover can anchor to a text selection. */
export function measurableFromRect(rect: RectLike) {
  const box = {
    x: rect.left,
    y: rect.top,
    left: rect.left,
    top: rect.top,
    width: rect.width,
    height: rect.height,
    right: rect.left + rect.width,
    bottom: rect.top + rect.height,
  };
  return { getBoundingClientRect: () => ({ ...box, toJSON: () => box }) as DOMRect };
}

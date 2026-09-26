<script lang="ts">
  import { Button, TextInput } from "@prismedia/ui-svelte";
  import ReaderContentsSheet from "./reader/ReaderContentsSheet.svelte";
  import type { Snippet } from "svelte";
  import { onMount, untrack } from "svelte";
  import {
    AlertTriangle,
    BookOpen,
    Bookmark,
    BookmarkCheck,
    Check,
    Pencil,
    Trash2,
    ChevronLeft,
    ChevronRight,
    List,
    Highlighter,
    Minus,
    NotebookPen,
    Plus,
    Rows3,
    X,
  } from "@lucide/svelte";
  import { apiAssetUrl as toApiUrl } from "$lib/api/orval-fetch";
  import ReaderShell from "$lib/components/reader/ReaderShell.svelte";
  import ReaderSelectionMenu, {
    type ReaderSelectionAction,
  } from "$lib/components/reader/ReaderSelectionMenu.svelte";
  import AnnotationEditor from "$lib/components/reader/AnnotationEditor.svelte";
  import type { ReaderNotice } from "$lib/components/reader/reader-notice";
  import { ANNOTATION_COLOR, type AnnotationColorCode } from "$lib/api/generated/codes";
  import type { BookAnnotation, BookBookmark } from "$lib/api/generated/model";
  import type { AnnotationDraft } from "$lib/reader/annotation-sync";
  import { ANNOTATION_COLOR_ORDER, annotationColorPaint } from "$lib/reader/annotation-colors";
  import { selectionAnchorRect, textQuoteFor } from "$lib/reader/epub-selection";
  import { bookmarkOnPage, pageStartLocator } from "$lib/reader/bookmark-page";
  import { findTextQuote, sameQuotedText } from "$lib/reader/annotation-reanchor";
  import type { BookmarkPage } from "$lib/reader/book-bookmark-store.svelte";

  type ReaderFlow = "paginated" | "scrolled";

  interface ReaderLocation {
    cfi: string | null;
    fraction: number;
    label: string | null;
  }

  interface TocEntry {
    label: string;
    href: string | null;
    subitems: TocEntry[];
  }

  interface Props {
    sourceUrl: string;
    contentType: string;
    title?: string;
    presentation?: "overlay" | "page";
    closeIcon?: "close" | "back";
    initialLocation?: string | null;
    initialFraction?: number | null;
    initialFlow?: ReaderFlow;
    onClose: () => void;
    onLocationChange?: (location: ReaderLocation) => void;
    onFlowChange?: (flow: ReaderFlow) => void;
    /** Optional transport from a companion rendition, such as the matched audiobook. */
    companionControls?: Snippet;
    /** Highlights to draw (already filtered to non-deleted). Omit to disable annotations. */
    annotations?: readonly BookAnnotation[];
    /** Called for a new highlight; returns the stored (optimistic) annotation so its editor can open. */
    onCreateAnnotation?: (draft: AnnotationDraft) => BookAnnotation | undefined;
    onUpdateAnnotation?: (annotation: BookAnnotation) => void;
    onDeleteAnnotation?: (annotation: BookAnnotation) => void;
    /** Non-blocking notice shown over the reader. */
    notice?: ReaderNotice | null;
    /** Bookmarks to list (already filtered to non-deleted). Omit to disable bookmarks. */
    bookmarks?: readonly BookBookmark[];
    onAddBookmark?: (page: BookmarkPage) => void;
    onRenameBookmark?: (bookmark: BookBookmark, label: string) => void;
    onDeleteBookmark?: (bookmark: BookBookmark) => void;
  }

  let {
    sourceUrl,
    contentType,
    title = "Book",
    presentation = "overlay",
    closeIcon = "close",
    initialLocation = null,
    initialFraction = null,
    initialFlow = "paginated",
    onClose,
    onLocationChange,
    onFlowChange,
    companionControls,
    annotations,
    onCreateAnnotation,
    onUpdateAnnotation,
    onDeleteAnnotation,
    notice = null,
    bookmarks,
    onAddBookmark,
    onRenameBookmark,
    onDeleteBookmark,
  }: Props = $props();

  const MIN_FONT = 80;
  const MAX_FONT = 200;

  let shell = $state<ReturnType<typeof ReaderShell>>();
  let stageEl = $state<HTMLDivElement>();
  // The foliate `<foliate-view>` custom element has no published types; treat as a loose handle.
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let view: any = null;
  let ready = $state(false);
  let errorMessage = $state<string | null>(null);
  let flow = $state<ReaderFlow>(untrack(() => initialFlow));
  let fontPercent = $state(100);
  let counterLabel = $state("");
  let atEnd = $state(false);
  let toc = $state.raw<TocEntry[]>([]);
  let tocOpen = $state(false);
  // Fixed-layout books (PDF) have no reflow, so font-size and paged/scrolled flow do not apply.
  let isFixedLayout = $state(false);

  const hasToc = $derived(toc.length > 0);

  // Neutral accent accent so in-book links read as interactive without the default deep-blue.
  function contentStyles(percent: number): string {
    // !important so the book's own stylesheet (which often colors links a hard blue) can't win.
    return `
      html { color-scheme: dark; font-size: ${percent}%; }
      html, body { background: transparent !important; color: #e7e7ea !important; }
      a, a:link, a:visited, a *,
      a[href], a[href]:link, a[href]:visited, a[href] * {
        color: #c7c9cc !important;
        text-decoration: underline;
        text-underline-offset: 2px;
      }
      a:hover, a:hover *, a[href]:hover, a[href]:hover * { color: #f7d59a !important; }
      img { background: transparent; }
    `;
  }

  function applyFlow(next: ReaderFlow) {
    view?.renderer?.setAttribute?.("flow", next === "scrolled" ? "scrolled" : "paginated");
  }

  function applyStyles() {
    view?.renderer?.setStyles?.(contentStyles(fontPercent));
  }

  function setFlow(next: ReaderFlow) {
    if (next === flow) return;
    flow = next;
    applyFlow(next);
    onFlowChange?.(next);
  }

  function changeFont(delta: number) {
    const next = Math.max(MIN_FONT, Math.min(MAX_FONT, fontPercent + delta));
    if (next === fontPercent) return;
    fontPercent = next;
    applyStyles();
  }

  /** Moves the open book to a CFI or a fraction (used to follow progress from another device). */
  export async function goToLocation(target: { cfi: string } | { fraction: number }) {
    try {
      if ("cfi" in target) await view?.goTo?.(target.cfi);
      else await view?.goToFraction?.(Math.max(0, Math.min(1, target.fraction)));
    } catch {
      // unresolvable target: stay where we are
    }
  }

  function goPrev() {
    view?.goLeft?.();
  }

  function goNext() {
    view?.goRight?.();
  }

  function normalizeToc(items: unknown): TocEntry[] {
    if (!Array.isArray(items)) return [];
    return items
      .map((item) => {
        const entry = item as { label?: unknown; href?: unknown; subitems?: unknown };
        const label = typeof entry.label === "string" ? entry.label.trim() : "";
        return {
          label,
          href: typeof entry.href === "string" ? entry.href : null,
          subitems: normalizeToc(entry.subitems),
        };
      })
      .filter((entry) => entry.label.length > 0 || entry.subitems.length > 0);
  }

  function openToc(entry: TocEntry) {
    if (!entry.href) return;
    tocOpen = false;
    try {
      view?.goTo?.(entry.href);
    } catch {
      // ignore navigation failures for malformed hrefs
    }
  }

  function handleRelocate(event: Event) {
    const detail = (event as CustomEvent).detail ?? {};
    const fraction = typeof detail.fraction === "number" ? detail.fraction : 0;
    const cfi = typeof detail.cfi === "string" ? detail.cfi : null;
    const label = detail.tocItem?.label?.trim() || null;
    currentPage = { cfi, fraction, label, index: currentSectionIndex() };
    lastFraction = fraction;
    atEnd = fraction >= 0.999;
    counterLabel = `${Math.round(fraction * 100)}%${label ? ` · ${label}` : ""}`;
    onLocationChange?.({ cfi, fraction, label });
    warmAdjacentSections();
  }

  // Warm the neighbouring sections so navigation is instant instead of flashing. For PDF each
  // section is a page whose load() parses the page and caches its rendered output (the slow part),
  // so pre-loading ahead removes the per-turn delay; for EPUB it primes the next section's content.
  function warmAdjacentSections() {
    const sections = view?.book?.sections;
    if (!Array.isArray(sections)) return;
    let current: number | undefined;
    try {
      current = view.renderer?.getContents?.()?.[0]?.index;
    } catch {
      current = undefined;
    }
    if (typeof current !== "number") return;
    // Look a couple ahead (and one behind for back-navigation); load() is cached, so repeats are free.
    for (const offset of [1, 2, -1]) {
      const section = sections[current + offset];
      try {
        void section?.load?.();
      } catch {
        // ignore warm-up failures; the page still renders on demand
      }
    }
  }

  // ── Annotations ─────────────────────────────────────────────────────────────
  // Selection is captured inside each loaded section document (foliate `load`), turned into a range
  // CFI plus a text quote, and offered through ReaderSelectionMenu. Highlights are drawn with
  // foliate's overlayer (`draw-annotation` / `create-overlay`), keyed by their CFI.

  interface ViewportRect {
    left: number;
    top: number;
    width: number;
    height: number;
  }

  interface PendingSelection {
    draft: Omit<AnnotationDraft, "color" | "note">;
    anchor: ViewportRect;
  }

  const LAST_COLOR_STORAGE_KEY = "nitrate.reader.lastHighlightColor";
  const SELECTION_SETTLE_MS = 350;

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let Overlayer: any = null;
  let lastFraction = 0;
  let pendingSelection = $state<PendingSelection | null>(null);
  let editing = $state<{ id: string; anchor: ViewportRect; focusNote: boolean } | null>(null);
  let lastColor = $state<AnnotationColorCode>(readLastColor());
  let annotationTapAt = 0;
  let selectionTimer: ReturnType<typeof setTimeout> | null = null;
  const drawn = new Map<string, { locator: string; color: string }>();

  const annotationsEnabled = $derived(Boolean(annotations && onCreateAnnotation) && !isFixedLayout);
  const annotationByLocator = $derived(new Map((annotations ?? []).map((item) => [item.locator, item])));
  const editingAnnotation = $derived(
    editing ? (annotations ?? []).find((item) => item.id === editing?.id) ?? null : null,
  );

  const selectionActions: ReaderSelectionAction[] = [
    { id: "highlight", label: "Highlight", icon: Highlighter, run: () => createFromSelection(false) },
    { id: "note", label: "Add Note", icon: NotebookPen, run: () => createFromSelection(true) },
  ];

  function readLastColor(): AnnotationColorCode {
    try {
      const stored = localStorage.getItem(LAST_COLOR_STORAGE_KEY);
      if (stored && (ANNOTATION_COLOR_ORDER as readonly string[]).includes(stored)) {
        return stored as AnnotationColorCode;
      }
    } catch {
      // storage unavailable (private mode); fall back to the default colour
    }
    return ANNOTATION_COLOR.yellow;
  }

  function rememberColor(color: AnnotationColorCode) {
    lastColor = color;
    try {
      localStorage.setItem(LAST_COLOR_STORAGE_KEY, color);
    } catch {
      // ignore storage failures; the colour still applies for this session
    }
  }

  function viewportRectOf(range: Range): ViewportRect {
    const frame = range.startContainer.ownerDocument?.defaultView?.frameElement?.getBoundingClientRect() ?? null;
    return selectionAnchorRect(range.getBoundingClientRect(), frame);
  }

  function handleSectionLoad(event: Event) {
    const { doc, index } = (event as CustomEvent).detail ?? {};
    // Section documents live in iframes (another realm), so check shape rather than `instanceof`.
    if (typeof doc?.getSelection !== "function" || typeof index !== "number") return;
    const sectionDoc = doc as Document;
    const settle = () => {
      if (selectionTimer) clearTimeout(selectionTimer);
      selectionTimer = setTimeout(() => captureSelection(sectionDoc, index), SELECTION_SETTLE_MS);
    };
    sectionDoc.addEventListener("selectionchange", settle);
    sectionDoc.addEventListener("pointerup", settle);
    sectionDoc.addEventListener("keyup", settle);
    sectionDoc.addEventListener("click", (clickEvent) => handleContentClick(sectionDoc, clickEvent));
  }

  function captureSelection(doc: Document, index: number) {
    if (!annotationsEnabled || editing) return;
    const selection = doc.getSelection();
    if (!selection || selection.isCollapsed || selection.rangeCount === 0) {
      pendingSelection = null;
      return;
    }
    const range = selection.getRangeAt(0);
    const quote = textQuoteFor(range);
    if (!quote.selectedText.trim()) return;
    let locator: string;
    try {
      locator = view.getCFI(index, range);
    } catch {
      return;
    }
    pendingSelection = {
      draft: { locator, spineIndex: index, position: lastFraction, ...quote },
      anchor: viewportRectOf(range),
    };
  }

  function clearSelection() {
    pendingSelection = null;
    try {
      view?.deselect?.();
    } catch {
      // ignore: the section may have unloaded
    }
  }

  function createFromSelection(openNote: boolean) {
    const pending = pendingSelection;
    if (!pending || !onCreateAnnotation) return;
    clearSelection();
    const existing = annotationByLocator.get(pending.draft.locator);
    const annotation = existing
      ? (onUpdateAnnotation?.({ ...existing, color: lastColor }), existing)
      : onCreateAnnotation({ ...pending.draft, color: lastColor, note: null });
    if (openNote && annotation) {
      editing = { id: annotation.id, anchor: pending.anchor, focusNote: true };
    }
  }

  function handleContentClick(doc: Document, event: MouseEvent) {
    // Centre taps toggle the reader chrome in paged mode (edge strips handle page turns). Deferred so
    // foliate's own overlay hit-test (`show-annotation`) and link handling run first.
    if (flow !== "paginated") return;
    const target = event.target as Element | null;
    if (target?.closest?.("a[href]")) return;
    setTimeout(() => {
      if (performance.now() - annotationTapAt < 400) return;
      if (!(doc.getSelection()?.isCollapsed ?? true)) return;
      if (pendingSelection || editing) return;
      shell?.toggleControls();
    }, 0);
  }

  function handleDrawAnnotation(event: Event) {
    const { draw, annotation } = (event as CustomEvent).detail ?? {};
    if (typeof draw !== "function" || !Overlayer) return;
    draw(Overlayer.highlight, { color: annotationColorPaint(annotation?.color ?? "") });
  }

  function handleCreateOverlay(event: Event) {
    const index = (event as CustomEvent).detail?.index;
    // foliate registers the new section's overlayer right after emitting this event; draw on the next tick.
    setTimeout(() => {
      for (const item of annotations ?? []) {
        if (Number(item.spineIndex) === index) void drawVerified(item);
      }
    }, 0);
  }

  const repairedThisSession = new Set<string>();

  function sectionDocument(index: number): Document | null {
    try {
      const contents = view?.renderer?.getContents?.() ?? [];
      return contents.find((entry: { index?: number }) => entry.index === index)?.doc ?? null;
    } catch {
      return null;
    }
  }

  /**
   * Draws a highlight only where its CFI still covers the highlighted text. If the book's file changed and the
   * CFI now points elsewhere (or nowhere), re-find the text quote in that section; when found, draw it there and
   * save the repaired CFI (once per session per highlight). When the text is gone, leave it undrawn.
   */
  async function drawVerified(annotation: BookAnnotation) {
    const index = Number(annotation.spineIndex);
    const doc = sectionDocument(index);
    if (!doc) return; // section not loaded; `create-overlay` draws it when it is
    let range: Range | null = null;
    try {
      const resolved = view?.resolveCFI?.(annotation.locator);
      if (resolved?.index === index) range = resolved.anchor(doc) ?? null;
    } catch {
      range = null;
    }
    if (range && sameQuotedText(range.toString(), annotation.selectedText)) {
      await drawAnnotation(annotation.locator, annotation.color);
      return;
    }
    if (repairedThisSession.has(annotation.id)) return;
    const found = findTextQuote(doc, annotation);
    if (!found) return;
    let repaired: string;
    try {
      repaired = view.getCFI(index, found);
    } catch {
      return;
    }
    repairedThisSession.add(annotation.id);
    if (repaired === annotation.locator) {
      await drawAnnotation(annotation.locator, annotation.color);
      return;
    }
    // Saving the repaired locator updates `annotations`, which redraws it through `syncDrawnAnnotations`.
    onUpdateAnnotation?.({ ...annotation, locator: repaired });
  }

  function handleShowAnnotation(event: Event) {
    const { value, range } = (event as CustomEvent).detail ?? {};
    const annotation = typeof value === "string" ? annotationByLocator.get(value) : undefined;
    if (!annotation || typeof range?.getBoundingClientRect !== "function") return;
    annotationTapAt = performance.now();
    pendingSelection = null;
    editing = { id: annotation.id, anchor: viewportRectOf(range as Range), focusNote: false };
  }

  async function drawAnnotation(locator: string, color: string) {
    try {
      await view?.addAnnotation?.({ value: locator, color });
    } catch {
      // unresolvable CFI (e.g. the file changed); the text quote is kept for later re-anchoring
    }
  }

  async function eraseAnnotation(locator: string) {
    try {
      await view?.deleteAnnotation?.({ value: locator });
    } catch {
      // ignore: nothing drawn for this locator
    }
  }

  function syncDrawnAnnotations(items: readonly BookAnnotation[]) {
    const byId = new Map(items.map((item) => [item.id, item]));
    const next = new Map(items.map((item) => [item.id, { locator: item.locator, color: item.color }]));
    for (const [id, previous] of drawn) {
      const current = next.get(id);
      if (!current || current.locator !== previous.locator) {
        void eraseAnnotation(previous.locator);
        drawn.delete(id);
      }
    }
    for (const [id, current] of next) {
      const previous = drawn.get(id);
      if (!previous || previous.color !== current.color || previous.locator !== current.locator) {
        void drawVerified(byId.get(id)!);
        drawn.set(id, current);
      }
    }
  }

  $effect(() => {
    const items = annotations ?? [];
    if (ready && annotationsEnabled) syncDrawnAnnotations(items);
  });

  function closeEditor() {
    editing = null;
  }

  // ── Bookmarks ───────────────────────────────────────────────────────────────
  let currentPage = $state<{ cfi: string | null; fraction: number; label: string | null; index: number | null }>({
    cfi: null,
    fraction: 0,
    label: null,
    index: null,
  });
  let renaming = $state<{ id: string; label: string } | null>(null);
  const bookmarksEnabled = $derived(Boolean(bookmarks && onAddBookmark) && !isFixedLayout);
  const pageBookmark = $derived(bookmarkOnPage(bookmarks ?? [], currentPage.cfi));

  function currentSectionIndex(): number | null {
    try {
      const index = view?.renderer?.getContents?.()?.[0]?.index;
      return typeof index === "number" ? index : null;
    } catch {
      return null;
    }
  }

  function toggleBookmark() {
    if (pageBookmark) {
      onDeleteBookmark?.(pageBookmark);
      return;
    }
    const locator = pageStartLocator(currentPage.cfi);
    if (!locator) return;
    onAddBookmark?.({
      locator,
      spineIndex: currentPage.index ?? 0,
      position: Math.max(0, Math.min(1, currentPage.fraction)),
      chapterLabel: currentPage.label,
    });
  }

  function openBookmark(bookmark: BookBookmark) {
    tocOpen = false;
    try {
      void view?.goTo?.(bookmark.locator);
    } catch {
      // unresolvable locator (file changed): stay put
    }
  }

  function saveRename(bookmark: BookBookmark) {
    if (!renaming) return;
    onRenameBookmark?.(bookmark, renaming.label);
    renaming = null;
  }

  // Comic-style gesture overlay (paged mode only). foliate columnizes the page into a single
  // wide iframe inside an inaccessible (closed shadow DOM) scroller, so tap coordinates can't be
  // read reliably from inside the content. Instead we capture pointer gestures on an overlay in
  // the top window — exactly like the comic reader — and drive foliate via goLeft/goRight.
  interface ReaderPointerGesture {
    pointerId: number;
    startX: number;
    startY: number;
    moved: boolean;
  }

  let readerPointerGesture: ReaderPointerGesture | null = null;
  const READER_SWIPE_THRESHOLD = 50;

  function handleGesturePointerDown(event: PointerEvent) {
    readerPointerGesture = {
      pointerId: event.pointerId,
      startX: event.clientX,
      startY: event.clientY,
      moved: false,
    };
    if (event.pointerType !== "mouse") {
      (event.currentTarget as HTMLElement).setPointerCapture?.(event.pointerId);
    }
  }

  function handleGesturePointerMove(event: PointerEvent) {
    if (!readerPointerGesture || readerPointerGesture.pointerId !== event.pointerId) return;
    const deltaX = event.clientX - readerPointerGesture.startX;
    const deltaY = event.clientY - readerPointerGesture.startY;
    if (Math.hypot(deltaX, deltaY) > 10) readerPointerGesture.moved = true;
  }

  function clearGesture() {
    readerPointerGesture = null;
  }

  function handleGesturePointerUp(event: PointerEvent, edge: "previous" | "next") {
    const gesture = readerPointerGesture;
    clearGesture();

    if (gesture && gesture.pointerId === event.pointerId) {
      const dx = event.clientX - gesture.startX;
      const dy = event.clientY - gesture.startY;
      const absX = Math.abs(dx);
      const absY = Math.abs(dy);

      // Touch swipe: horizontal turns the page, a downward swipe dismisses — matching the comic reader.
      if (event.pointerType !== "mouse" && Math.max(absX, absY) > READER_SWIPE_THRESHOLD) {
        if (absX > absY * 1.3) {
          if (dx < 0) goNext();
          else goPrev();
          return;
        }
        if (absY > absX * 1.3 && dy > 0) {
          onClose();
          return;
        }
        return;
      }

      if (gesture.moved) return;
    }

    if (event.pointerType === "mouse") return;
    if (edge === "previous") goPrev();
    else goNext();
  }

  onMount(() => {
    let disposed = false;

    void (async () => {
      try {
        // Registers the <foliate-view> custom element (vendored, client-only).
        await import("$lib/vendor/foliate-js/view.js");
        Overlayer = (await import("$lib/vendor/foliate-js/overlayer.js")).Overlayer;
        if (disposed || !stageEl) return;

        const absoluteUrl = toApiUrl(sourceUrl) ?? sourceUrl;
        const response = await fetch(absoluteUrl);
        if (!response.ok) {
          throw new Error(`Failed to load book (${response.status})`);
        }
        const blob = await response.blob();
        const fileName = contentType.includes("pdf") ? "book.pdf" : "book.epub";
        const file = new File([blob], fileName, { type: contentType });
        if (disposed) return;

        view = document.createElement("foliate-view");
        view.style.width = "100%";
        view.style.height = "100%";
        stageEl.append(view);

        await view.open(file);
        if (disposed) return;

        toc = normalizeToc(view.book?.toc);
        isFixedLayout = Boolean(view.isFixedLayout);
        view.addEventListener("relocate", handleRelocate);
        view.addEventListener("load", handleSectionLoad);
        view.addEventListener("draw-annotation", handleDrawAnnotation);
        view.addEventListener("create-overlay", handleCreateOverlay);
        view.addEventListener("show-annotation", handleShowAnnotation);
        applyFlow(flow);
        applyStyles();
        const boundedInitialFraction = typeof initialFraction === "number" && Number.isFinite(initialFraction)
          ? Math.max(0, Math.min(1, initialFraction))
          : null;
        const initialTarget = initialLocation ?? (boundedInitialFraction !== null
          ? { fraction: boundedInitialFraction }
          : undefined);
        await view.init({
          lastLocation: initialTarget,
          showTextStart: !initialTarget,
        });
        ready = true;
      } catch (err) {
        if (!disposed) {
          errorMessage = err instanceof Error ? err.message : String(err);
        }
      }
    })();

    return () => {
      disposed = true;
      try {
        view?.removeEventListener?.("relocate", handleRelocate);
        view?.removeEventListener?.("load", handleSectionLoad);
        view?.removeEventListener?.("draw-annotation", handleDrawAnnotation);
        view?.removeEventListener?.("create-overlay", handleCreateOverlay);
        view?.removeEventListener?.("show-annotation", handleShowAnnotation);
        if (selectionTimer) clearTimeout(selectionTimer);
        view?.close?.();
        view?.remove?.();
      } catch {
        // ignore teardown races
      }
      view = null;
    };
  });
</script>

<ReaderShell
  bind:this={shell}
  {title}
  {presentation}
  {closeIcon}
  {onClose}
  onPrev={goPrev}
  onNext={goNext}
  onActivate={goNext}
  {notice}
>
  {#snippet counter()}{counterLabel}{/snippet}

  {#snippet controls()}
    {#if companionControls}
      {@render companionControls()}
    {/if}
    {#if bookmarksEnabled}
      <Button variant="outline" size="sm"
        type="button"
        onclick={toggleBookmark}
        aria-pressed={Boolean(pageBookmark)}
        class="aria-pressed:bg-accent aria-pressed:text-accent-foreground"
        aria-label={pageBookmark ? "Remove bookmark" : "Bookmark this page"}
        title={pageBookmark ? "Remove bookmark" : "Bookmark this page"}
      >
        {#if pageBookmark}
          <BookmarkCheck class="h-4 w-4" />
        {:else}
          <Bookmark class="h-4 w-4" />
        {/if}
      </Button>
    {/if}
    {#if hasToc || bookmarksEnabled}
      <Button variant="outline" size="sm"
        type="button"
        onclick={() => (tocOpen = true)}
        class="aria-pressed:bg-accent aria-pressed:text-accent-foreground"
        aria-label="Table of contents"
        title="Contents"
      >
        <List class="h-4 w-4" />
        <span class="hidden sm:inline">Contents</span>
      </Button>
    {/if}

    {#if !isFixedLayout}
      <div class="flex items-center gap-1 border-l border-border-subtle pl-2">
        <Button variant="outline" size="sm"
          type="button"
          onclick={() => setFlow("paginated")}
          aria-pressed={flow === "paginated"}
          class="aria-pressed:bg-accent aria-pressed:text-accent-foreground"
          aria-label="Paged reading"
          title="Paged reading"
        >
          <BookOpen class="h-4 w-4" />
          <span class="hidden sm:inline">Paged</span>
        </Button>
        <Button variant="outline" size="sm"
          type="button"
          onclick={() => setFlow("scrolled")}
          aria-pressed={flow === "scrolled"}
          class="aria-pressed:bg-accent aria-pressed:text-accent-foreground"
          aria-label="Scrolled reading"
          title="Scrolled reading"
        >
          <Rows3 class="h-4 w-4" />
          <span class="hidden sm:inline">Scroll</span>
        </Button>
      </div>
    {/if}

    {#if !isFixedLayout}
    <div class="hidden items-center gap-1 border-l border-border-subtle pl-2 sm:flex">
      <Button variant="outline" size="sm"
        type="button"
        onclick={() => changeFont(-10)}
        disabled={fontPercent <= MIN_FONT}
        class="aria-pressed:bg-accent aria-pressed:text-accent-foreground"
        aria-label="Decrease font size"
        title="Decrease font size"
      >
        <Minus class="h-4 w-4" />
      </Button>
      <span class="font-mono text-[0.62rem] tabular-nums text-text-muted">{fontPercent}%</span>
      <Button variant="outline" size="sm"
        type="button"
        onclick={() => changeFont(10)}
        disabled={fontPercent >= MAX_FONT}
        class="aria-pressed:bg-accent aria-pressed:text-accent-foreground"
        aria-label="Increase font size"
        title="Increase font size"
      >
        <Plus class="h-4 w-4" />
      </Button>
    </div>
    {/if}
  {/snippet}

  <div class="book-reader-stage" bind:this={stageEl}>
    {#if ready && flow === "paginated"}
      <!-- Edge strips turn pages on touch; the middle is left to the book so text can be selected. -->
      {#each ["previous", "next"] as const as edge (edge)}
        <!-- svelte-ignore a11y_no_static_element_interactions -->
        <div
          class="book-reader-edge"
          class:book-reader-edge-previous={edge === "previous"}
          class:book-reader-edge-next={edge === "next"}
          onpointerdown={handleGesturePointerDown}
          onpointermove={handleGesturePointerMove}
          onpointerup={(event) => handleGesturePointerUp(event, edge)}
          onpointercancel={clearGesture}
        ></div>
      {/each}
    {/if}

    {#if annotationsEnabled}
      <ReaderSelectionMenu
        anchor={pendingSelection?.anchor ?? null}
        actions={selectionActions}
        onDismiss={clearSelection}
      />
      <AnnotationEditor
        anchor={editingAnnotation ? editing?.anchor ?? null : null}
        color={editingAnnotation?.color ?? lastColor}
        note={editingAnnotation?.note ?? null}
        focusNote={editing?.focusNote ?? false}
        onChangeColor={(color) => {
          rememberColor(color);
          if (editingAnnotation) onUpdateAnnotation?.({ ...editingAnnotation, color });
        }}
        onSaveNote={(note) => {
          if (editingAnnotation) onUpdateAnnotation?.({ ...editingAnnotation, note: note.trim() ? note : null });
          closeEditor();
        }}
        onDelete={() => {
          if (editingAnnotation) onDeleteAnnotation?.(editingAnnotation);
          closeEditor();
        }}
        onDismiss={closeEditor}
      />
    {/if}

    {#if ready}
      <Button variant="outline" size="sm"
        type="button"
        onclick={goPrev}
        data-reader-control
        class="absolute top-1/2 z-10 hidden size-11 -translate-y-1/2 sm:inline-flex left-2 sm:left-3"
        aria-label="Previous page"
        title="Previous (←)"
      >
        <ChevronLeft class="h-6 w-6" />
      </Button>
      <Button variant="outline" size="sm"
        type="button"
        onclick={goNext}
        data-reader-control
        class="absolute top-1/2 z-10 hidden size-11 -translate-y-1/2 sm:inline-flex right-2 sm:right-3"
        aria-label="Next page"
        title="Next (→)"
      >
        <ChevronRight class="h-6 w-6" />
      </Button>
      {#if atEnd}
        <div class="book-reader-end" data-reader-control>End of book</div>
      {/if}
    {/if}

    {#if errorMessage}
      <div class="book-reader-message">
        <AlertTriangle class="h-5 w-5" />
        <p>{errorMessage}</p>
      </div>
    {:else if !ready}
      <div class="book-reader-message">
        <p>Opening book…</p>
      </div>
    {/if}
  </div>
</ReaderShell>

<ReaderContentsSheet
  open={tocOpen}
  onClose={() => tocOpen = false}
  bookmarks={bookmarksEnabled ? bookmarkItems : undefined}
  bookmarkCount={bookmarks?.length ?? 0}
>
  {@render tocItems(toc, 0)}
</ReaderContentsSheet>

{#snippet bookmarkItems()}
  {#if !bookmarks?.length}
    <p class="px-3 py-4 text-sm text-text-muted">No bookmarks yet. Use the bookmark button in the top bar to mark a page.</p>
  {:else}
    <ul class="flex flex-col gap-1">
      {#each bookmarks as bookmark (bookmark.id)}
        <li class="flex items-center gap-1">
          {#if renaming?.id === bookmark.id}
            <TextInput
              size="sm"
              class="min-w-0 flex-1"
              aria-label="Bookmark label"
              placeholder={bookmark.chapterLabel ?? "Bookmark"}
              bind:value={renaming.label}
              onkeydown={(event: KeyboardEvent) => { if (event.key === "Enter") saveRename(bookmark); }}
            />
            <Button variant="ghost" size="icon-sm" type="button" aria-label="Save label" onclick={() => saveRename(bookmark)}>
              <Check class="h-4 w-4" />
            </Button>
          {:else}
            <Button variant="ghost"
              class="h-auto min-w-0 flex-1 justify-start whitespace-normal py-2 text-left"
              onclick={() => openBookmark(bookmark)}
            >
              <span class="flex min-w-0 flex-col">
                <span>{bookmark.label ?? bookmark.chapterLabel ?? "Bookmark"}</span>
                <span class="font-mono text-[0.6rem] uppercase tracking-[0.14em] text-text-muted">
                  {Math.round(Number(bookmark.position) * 100)}%{bookmark.label && bookmark.chapterLabel ? ` · ${bookmark.chapterLabel}` : ""}
                </span>
              </span>
            </Button>
            <Button variant="ghost" size="icon-sm" type="button" aria-label="Rename bookmark"
              onclick={() => (renaming = { id: bookmark.id, label: bookmark.label ?? "" })}
            >
              <Pencil class="h-4 w-4" />
            </Button>
          {/if}
          <Button variant="ghost" size="icon-sm" type="button" aria-label="Delete bookmark" onclick={() => onDeleteBookmark?.(bookmark)}>
            <Trash2 class="h-4 w-4" />
          </Button>
        </li>
      {/each}
    </ul>
  {/if}
{/snippet}

{#snippet tocItems(items: TocEntry[], depth: number)}
  {#each items as entry (entry.label + (entry.href ?? ""))}
    <Button variant="ghost"
      class="h-auto w-full justify-start whitespace-normal py-2 text-left"
      style={`padding-left: ${0.85 + depth * 0.9}rem`}
      disabled={!entry.href}
      onclick={() => openToc(entry)}
    >
      {entry.label}
    </Button>
    {#if entry.subitems.length > 0}
      {@render tocItems(entry.subitems, depth + 1)}
    {/if}
  {/each}
{/snippet}

<style>
  .book-reader-stage {
    position: absolute;
    inset: 0;
    display: block;
    background: #0b0c0f;
    overflow: hidden;
  }

  /* On desktop the edge arrows are visible, so inset the page content (a static, full-size
     child) clear of them. On mobile there are no arrows and the page uses the full width. */
  @media (min-width: 640px) {
    .book-reader-stage {
      padding: 0 3.75rem;
    }
  }

  .book-reader-message {
    position: absolute;
    inset: 0;
    display: grid;
    place-items: center;
    gap: 0.6rem;
    color: var(--color-text-secondary);
    text-align: center;
    pointer-events: none;
  }

  /* Touch tap/swipe strips at the page edges in paged mode. The middle of the page is left to the
     book so text can be selected; centre taps toggle the chrome from inside the content instead.
     Sits below the edge nav buttons (z-index 10) so those still receive their own clicks. */
  .book-reader-edge {
    position: absolute;
    top: 0;
    bottom: 0;
    width: 18%;
    z-index: 5;
    touch-action: none;
  }

  .book-reader-edge-previous {
    left: 0;
  }

  .book-reader-edge-next {
    right: 0;
  }

  /* Mouse users page with the arrow buttons and keys; let clicks reach the text for selection. */
  @media (hover: hover) and (pointer: fine) {
    .book-reader-edge {
      pointer-events: none;
    }
  }

  .book-reader-end {
    position: absolute;
    bottom: 1.25rem;
    left: 50%;
    transform: translateX(-50%);
    z-index: 12;
    border: 1px solid var(--color-border-accent);
    border-radius: var(--radius-sm);
    background: var(--color-overlay-heavy);
    padding: 0.35rem 0.8rem;
    color: var(--color-text-accent-bright);
    font-family: var(--font-mono, monospace);
    font-size: 0.62rem;
    letter-spacing: 0.16em;
    text-transform: uppercase;
    backdrop-filter: blur(var(--glass-blur-sm));
  }

</style>

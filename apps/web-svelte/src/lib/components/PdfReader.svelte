<script lang="ts">
  import { Button, buttonVariants } from "@prismedia/ui-svelte";
  import ReaderContentsSheet from "./reader/ReaderContentsSheet.svelte";
  import { onMount, tick } from "svelte";
  import {
    AlertTriangle, Bookmark, BookmarkCheck, ChevronLeft, ChevronRight, Download, Highlighter,
    List, Maximize, Minus, NotebookPen, Plus, Search, StretchVertical, BookOpen, Rows3,
  } from "@lucide/svelte";
  import { apiAssetUrl as toApiUrl } from "$lib/api/orval-fetch";
  import { comicTapZone } from "$lib/components/comic-reader";
  import ReaderShell from "$lib/components/reader/ReaderShell.svelte";
  import type { ReaderNotice } from "$lib/components/reader/reader-notice";
  import ReaderSelectionMenu, { type ReaderSelectionAction } from "$lib/components/reader/ReaderSelectionMenu.svelte";
  import AnnotationEditor from "$lib/components/reader/AnnotationEditor.svelte";
  import PdfHighlightLayer from "$lib/components/reader/PdfHighlightLayer.svelte";
  import PdfSearchPanel from "$lib/components/reader/PdfSearchPanel.svelte";
  import ReaderBookmarkList from "$lib/components/reader/ReaderBookmarkList.svelte";
  import type { AnnotationColorCode } from "$lib/api/generated/codes";
  import type { BookAnnotation, BookBookmark } from "$lib/api/generated/model";
  import type { AnnotationDraft } from "$lib/reader/annotation-sync";
  import { readLastHighlightColor, rememberHighlightColor } from "$lib/reader/annotation-colors";
  import { textQuoteFor } from "$lib/reader/epub-selection";
  import { computeSelectionRects } from "$lib/reader/pdf-selection";
  import { buildPdfLocator, parsePdfLocator } from "$lib/reader/pdf-locator";
  import { ReaderPreferencesStore } from "$lib/reader/reader-preferences-store.svelte";
  import { THEME_COLORS } from "$lib/reader/reader-content-styles";
  import { buildLinkLayer, normalizeOutline, resolveDestPage, type TocEntry } from "$lib/reader/pdf-outline";

  type ReaderFlow = "scrolled" | "paged";

  interface Props {
    sourceUrl: string;
    title?: string;
    presentation?: "overlay" | "page";
    closeIcon?: "close" | "back";
    initialPage?: number;
    onClose: () => void;
    onPageChange?: (page: number, pageCount: number) => void;
    /** Non-blocking notice shown over the reader. */
    notice?: ReaderNotice | null;
    /** Book Entity id, threaded into the selection menu's lookup source context (Add to Vocabulary). */
    bookId?: string | null;
    /** Highlights to draw (already filtered to non-deleted, locatorKind "pdf"). Omit to disable annotations. */
    annotations?: readonly BookAnnotation[];
    onCreateAnnotation?: (draft: AnnotationDraft) => BookAnnotation | undefined;
    onUpdateAnnotation?: (annotation: BookAnnotation) => void;
    onDeleteAnnotation?: (annotation: BookAnnotation) => void;
    /** Bookmarks to list (already filtered to non-deleted). Omit to disable bookmarks. */
    bookmarks?: readonly BookBookmark[];
    onAddBookmark?: (page: number) => void;
    onRenameBookmark?: (bookmark: BookBookmark, label: string) => void;
    onDeleteBookmark?: (bookmark: BookBookmark) => void;
  }

  let {
    sourceUrl,
    title = "Document",
    presentation = "overlay",
    closeIcon = "close",
    initialPage = 0,
    onClose,
    onPageChange,
    notice = null,
    bookId = null,
    annotations,
    onCreateAnnotation,
    onUpdateAnnotation,
    onDeleteAnnotation,
    bookmarks,
    onAddBookmark,
    onRenameBookmark,
    onDeleteBookmark,
  }: Props = $props();

  let shell = $state<ReturnType<typeof ReaderShell>>();
  let scrollEl = $state<HTMLDivElement>();
  let ready = $state(false);
  let errorMessage = $state<string | null>(null);
  let pageCount = $state(0);
  let currentPage = $state(0);
  let toc = $state.raw<TocEntry[]>([]);
  let tocOpen = $state(false);
  const MIN_SCALE = 0.25;
  const MAX_SCALE = 6;
  let scale = $state(1); // also used to normalize highlight rects to the unscaled page viewport
  let gapless = $state(false);
  const zoomPercent = $derived(Math.round(scale * 100));
  const downloadHref = $derived(toApiUrl(sourceUrl));

  const hasToc = $derived(toc.length > 0);
  const pageIndexes = $derived(Array.from({ length: pageCount }, (_, i) => i));

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let pdf: any = null;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let pdfjsLib: any = null;
  let wrappers: HTMLDivElement[] = [];
  // Imperative canvas/text/link layers attach here, leaving `.pdf-page` free for the highlight overlay.
  let canvasHosts: HTMLDivElement[] = [];
  const rendered = new Set<number>();
  const rendering = new Set<number>();
  const prefsStore = new ReaderPreferencesStore();
  let observer: IntersectionObserver | null = null;
  // Unscaled page-1 dimensions, used to derive fit scales and placeholder sizes (the scroll
  // height — and each page's offsetTop — must be correct before pages lazily render so resume works).
  let pageBaseW = 0;
  let pageBaseH = 0;
  let baseWidth = 0;
  let baseHeight = 0;

  function fitWidthScale(): number {
    if (!scrollEl || pageBaseW <= 0) return scale || 1;
    const available = Math.min(scrollEl.clientWidth - 24, 1400);
    return available > 0 ? available / pageBaseW : 1;
  }

  function fitPageScale(): number {
    if (!scrollEl || pageBaseW <= 0 || pageBaseH <= 0) return scale || 1;
    const availW = scrollEl.clientWidth - 24;
    const availH = scrollEl.clientHeight - 24;
    return Math.min(availW / pageBaseW, availH / pageBaseH);
  }

  function clampScale(value: number): number {
    return Math.max(MIN_SCALE, Math.min(MAX_SCALE, value));
  }

  // Re-render the visible pages at the current scale and keep the current page in view.
  function applyScale() {
    if (pageBaseW <= 0) return;
    baseWidth = pageBaseW * scale;
    baseHeight = pageBaseH * scale;
    for (const wrapper of wrappers) {
      if (wrapper) {
        wrapper.style.width = `${baseWidth}px`;
        wrapper.style.height = `${baseHeight}px`;
      }
    }
    for (const index of [...rendered]) unloadPage(index);
    rendered.clear();
    // Re-observe so the IntersectionObserver re-renders whatever is currently on screen.
    observer?.disconnect();
    for (const wrapper of wrappers) if (wrapper) observer?.observe(wrapper);
    scrollToPage(currentPage, "auto");
  }

  function setScale(next: number) {
    const clamped = clampScale(next);
    if (Math.abs(clamped - scale) < 0.001) return;
    scale = clamped;
    applyScale();
  }

  const fitWidth = () => setScale(fitWidthScale());
  const fitPage = () => setScale(fitPageScale());
  const zoomIn = () => setScale(scale * 1.2);
  const zoomOut = () => setScale(scale / 1.2);

  function toggleGap() {
    gapless = !gapless;
  }

  // ── Paged mode (single page per view via scroll-snap + comic-style gestures) ──
  let flow = $state<ReaderFlow>("scrolled");

  function setFlow(next: ReaderFlow) {
    if (next === flow) return;
    flow = next;
    if (next === "paged") setScale(fitPageScale());
    else setScale(fitWidthScale());
  }

  // Tap handling via a plain click (so it never blocks native scroll, scroll-snap swipe, or wheel).
  // A real tap only fires `click` when the finger doesn't move, so this never triggers mid-scroll.
  function handleStageClick(event: MouseEvent) {
    const target = event.target as HTMLElement;
    if (target?.closest?.("[data-reader-control]")) return;
    // Let link taps navigate without toggling the chrome.
    if (target?.closest?.("a")) return;
    // Scrolled mode has no page turns, so any clean tap just toggles the toolbar.
    if (flow !== "paged") {
      shell?.toggleControls();
      return;
    }
    // Paged mode: the text layer is non-interactive, so taps land here — sides turn, centre toggles.
    const rect = (event.currentTarget as HTMLElement).getBoundingClientRect();
    const zone = comicTapZone(event.clientX - rect.left, rect.width);
    if (zone === "previous") goPrev();
    else if (zone === "next") goNext();
    else shell?.toggleControls();
  }

  // ── In-document search (Sheet UI mirroring BookSearchPanel; see pdf-text-search.ts) ──
  let searchOpen = $state(false);

  function toggleSearch() {
    searchOpen = !searchOpen;
  }

  // ── Annotations (highlight/note) ───────────────────────────────────────────
  const SELECTION_SETTLE_MS = 350;
  interface ViewportRect { left: number; top: number; width: number; height: number }
  interface PendingSelection { draft: Omit<AnnotationDraft, "color" | "note">; anchor: ViewportRect }
  let pendingSelection = $state<PendingSelection | null>(null);
  let editing = $state<{ id: string; anchor: ViewportRect; focusNote: boolean } | null>(null);
  let lastColor = $state<AnnotationColorCode>(readLastHighlightColor());
  let selectionTimer: ReturnType<typeof setTimeout> | null = null;

  const annotationsEnabled = $derived(Boolean(annotations && onCreateAnnotation));
  const annotationByLocator = $derived(new Map((annotations ?? []).map((item) => [item.locator, item])));
  const editingAnnotation = $derived(
    editing ? (annotations ?? []).find((item) => item.id === editing?.id) ?? null : null,
  );
  const selectionActions: ReaderSelectionAction[] = [
    { id: "highlight", label: "Highlight", icon: Highlighter, run: () => createFromSelection(false) },
    { id: "note", label: "Add Note", icon: NotebookPen, run: () => createFromSelection(true) },
  ];

  function handleSelectionSettle() {
    if (selectionTimer) clearTimeout(selectionTimer);
    selectionTimer = setTimeout(captureSelection, SELECTION_SETTLE_MS);
  }

  function captureSelection() {
    if (!annotationsEnabled || editing || flow === "paged") return;
    const selection = document.getSelection();
    if (!selection || selection.isCollapsed || selection.rangeCount === 0) {
      pendingSelection = null;
      return;
    }
    const range = selection.getRangeAt(0);
    const pageEl = (range.startContainer.parentElement ?? null)?.closest<HTMLElement>("[data-page-index]");
    const page = pageEl ? Number(pageEl.dataset.pageIndex) : NaN;
    if (!pageEl || Number.isNaN(page)) return;
    const rects = computeSelectionRects(range, pageEl, scale);
    const quote = textQuoteFor(range);
    if (rects.length === 0 || !quote.selectedText.trim()) return;
    pendingSelection = {
      draft: {
        locator: buildPdfLocator({ page, rects }),
        spineIndex: page,
        position: Math.max(0, Math.min(1, rects[0]?.y ?? 0)),
        ...quote,
      },
      anchor: range.getBoundingClientRect(),
    };
  }

  function clearSelection() {
    pendingSelection = null;
    try {
      document.getSelection()?.removeAllRanges();
    } catch {
      // ignore
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
    if (openNote && annotation) editing = { id: annotation.id, anchor: pending.anchor, focusNote: true };
  }

  function handleHighlightTap(annotation: BookAnnotation, anchor: ViewportRect) {
    pendingSelection = null;
    editing = { id: annotation.id, anchor, focusNote: false };
  }

  function annotationsForPage(index: number): BookAnnotation[] {
    return (annotations ?? []).filter((item) => {
      const locator = parsePdfLocator(item.locator);
      return locator?.page === index;
    });
  }

  // ── Bookmarks (page-based: locator kind "pdf", empty rects) ─────────────────
  const bookmarksEnabled = $derived(Boolean(bookmarks && onAddBookmark));
  const pageBookmark = $derived((bookmarks ?? []).find((b) => Number(b.spineIndex) === currentPage));
  function toggleBookmark() {
    if (pageBookmark) onDeleteBookmark?.(pageBookmark);
    else onAddBookmark?.(currentPage);
  }

  function openBookmark(bookmark: BookBookmark) {
    tocOpen = false;
    scrollToPage(Number(bookmark.spineIndex));
  }

  // ── Reader theme (M6): chrome background only, never the page canvases ──
  $effect(() => {
    const theme = prefsStore.value.theme;
    if (theme === "light" || theme === "sepia" || theme === "dark") {
      scrollEl?.style.setProperty("--pdf-stage-background", THEME_COLORS[theme].background);
    } else {
      scrollEl?.style.removeProperty("--pdf-stage-background");
    }
  });

  // Pinch-to-zoom: two-finger gesture scales the page column live via CSS transform, then commits
  // to a crisp re-render on release. One-finger scrolling stays native (touch-action: pan-y).
  const pinchPointers = new Map<number, { x: number; y: number }>();
  let pinchStartDist = 0;
  let pinchStartScale = 1;
  let pinching = $state(false);
  let pinchRatio = $state(1);

  function pointerDistance(): number {
    const pts = [...pinchPointers.values()];
    return pts.length < 2 ? 0 : Math.hypot(pts[0].x - pts[1].x, pts[0].y - pts[1].y);
  }

  function handleStagePointerDown(event: PointerEvent) {
    if (event.pointerType === "mouse" || flow === "paged") return;
    pinchPointers.set(event.pointerId, { x: event.clientX, y: event.clientY });
    if (pinchPointers.size === 2) {
      pinching = true;
      pinchStartDist = pointerDistance();
      pinchStartScale = scale;
      pinchRatio = 1;
    }
  }

  function handleStagePointerMove(event: PointerEvent) {
    if (!pinchPointers.has(event.pointerId)) return;
    pinchPointers.set(event.pointerId, { x: event.clientX, y: event.clientY });
    if (pinching && pinchPointers.size === 2) {
      event.preventDefault();
      const dist = pointerDistance();
      pinchRatio = pinchStartDist > 0 ? dist / pinchStartDist : 1;
    }
  }

  function handleStagePointerEnd(event: PointerEvent) {
    if (!pinchPointers.delete(event.pointerId)) return;
    if (pinching && pinchPointers.size < 2) {
      pinching = false;
      const target = clampScale(pinchStartScale * pinchRatio);
      pinchRatio = 1;
      setScale(target);
    }
  }

  async function renderPage(index: number) {
    if (!pdf || rendered.has(index) || rendering.has(index)) return;
    const wrapper = wrappers[index];
    const host = canvasHosts[index];
    if (!wrapper || !host) return;
    rendering.add(index);
    try {
      const page = await pdf.getPage(index + 1);
      const cssViewport = page.getViewport({ scale });
      const dpr = Math.min(globalThis.devicePixelRatio || 1, 2);
      const deviceViewport = page.getViewport({ scale: scale * dpr });

      wrapper.style.height = `${cssViewport.height}px`;
      wrapper.style.width = `${cssViewport.width}px`;

      const canvas = document.createElement("canvas");
      canvas.width = Math.ceil(deviceViewport.width);
      canvas.height = Math.ceil(deviceViewport.height);
      canvas.style.width = `${cssViewport.width}px`;
      canvas.style.height = `${cssViewport.height}px`;
      canvas.className = "pdf-canvas";
      const ctx = canvas.getContext("2d");
      await page.render({ canvasContext: ctx, viewport: deviceViewport }).promise;

      const textLayerDiv = document.createElement("div");
      textLayerDiv.className = "pdf-text-layer";
      textLayerDiv.style.width = `${cssViewport.width}px`;
      textLayerDiv.style.height = `${cssViewport.height}px`;
      textLayerDiv.style.setProperty("--scale-factor", String(scale));
      const textLayer = new pdfjsLib.TextLayer({
        textContentSource: page.streamTextContent(),
        container: textLayerDiv,
        viewport: cssViewport,
      });
      await textLayer.render();

      const linkLayer = await buildLinkLayer(page, cssViewport, (dest) => resolveDestPage(pdf, dest), scrollToPage);

      host.replaceChildren(canvas, textLayerDiv, ...(linkLayer ? [linkLayer] : []));
      rendered.add(index);
    } catch {
      // leave the placeholder; it will retry if it scrolls back into view
    } finally {
      rendering.delete(index);
    }
  }

  function unloadPage(index: number) {
    if (!rendered.has(index)) return;
    canvasHosts[index]?.replaceChildren();
    rendered.delete(index);
  }

  function updateCurrentPage() {
    if (!scrollEl || pageCount === 0) return;
    const mid = scrollEl.scrollTop + scrollEl.clientHeight / 2;
    let next = currentPage;
    for (let i = 0; i < wrappers.length; i++) {
      const w = wrappers[i];
      if (w && w.offsetTop <= mid) next = i;
      else break;
    }
    if (next !== currentPage) {
      currentPage = next;
      onPageChange?.(currentPage, pageCount);
    }
  }

  let scrollRaf = 0;
  function handleScroll() {
    if (scrollRaf) return;
    scrollRaf = requestAnimationFrame(() => {
      scrollRaf = 0;
      updateCurrentPage();
    });
  }

  function scrollToPage(index: number, behavior: ScrollBehavior = "smooth") {
    const wrapper = wrappers[Math.max(0, Math.min(index, wrappers.length - 1))];
    if (wrapper && scrollEl) {
      scrollEl.scrollTo({ top: wrapper.offsetTop - 8, behavior });
    }
  }

  /** Moves to a zero-based page (used to follow progress from another device). */
  export function goToPage(page: number) {
    scrollToPage(page, "auto");
  }

  function goPrev() {
    scrollToPage(currentPage - 1);
  }

  function goNext() {
    scrollToPage(currentPage + 1);
  }

  function openToc(entry: TocEntry) {
    if (entry.pageIndex === null) return;
    tocOpen = false;
    scrollToPage(entry.pageIndex);
  }

  onMount(() => {
    let disposed = false;

    void prefsStore.load();
    document.addEventListener("selectionchange", handleSelectionSettle);

    void (async () => {
      try {
        // The legacy build bundles core-js polyfills (e.g. Map.prototype.getOrInsertComputed) the
        // non-legacy build assumes the runtime already has, so PDFs still render on older browsers
        // (older iPad Safari, Chrome/Edge that haven't shipped the newest Map methods).
        pdfjsLib = await import("pdfjs-dist/legacy/build/pdf.mjs");
        const workerModule = await import("pdfjs-dist/legacy/build/pdf.worker.min.mjs?url");
        pdfjsLib.GlobalWorkerOptions.workerSrc = (workerModule as { default: string }).default;

        const absoluteUrl = toApiUrl(sourceUrl) ?? sourceUrl;
        const response = await fetch(absoluteUrl);
        if (!response.ok) throw new Error(`Failed to load PDF (${response.status})`);
        const data = await response.arrayBuffer();
        if (disposed) return;

        pdf = await pdfjsLib.getDocument({ data }).promise;
        if (disposed) return;
        pageCount = pdf.numPages;

        // Size placeholders from the first page so the scroll height (and each page's offsetTop)
        // is correct before pages lazily render — required for resume and the scrollbar.
        try {
          const first = await pdf.getPage(1);
          const vp = first.getViewport({ scale: 1 });
          pageBaseW = vp.width;
          pageBaseH = vp.height;
          scale = clampScale(fitWidthScale());
          baseWidth = pageBaseW * scale;
          baseHeight = pageBaseH * scale;
        } catch {
          baseWidth = scrollEl ? Math.min(scrollEl.clientWidth - 24, 1100) : 800;
          baseHeight = baseWidth * 1.414;
        }

        // Resolve the outline (chapters) to page indexes.
        try {
          const outline = await pdf.getOutline();
          if (outline?.length) {
            toc = await normalizeOutline(outline, async (dest) => {
              const explicit = typeof dest === "string" ? await pdf.getDestination(dest) : dest;
              if (!Array.isArray(explicit) || !explicit[0]) return null;
              return await pdf.getPageIndex(explicit[0]);
            });
          }
        } catch {
          /* outline is optional */
        }

        ready = true;
        await tick();
        if (disposed) return;

        // Size placeholders and observe them for lazy rendering.
        for (const wrapper of wrappers) {
          if (wrapper) {
            wrapper.style.width = `${baseWidth}px`;
            wrapper.style.height = `${baseHeight}px`;
          }
        }
        observer = new IntersectionObserver(
          (entries) => {
            for (const e of entries) {
              const index = Number((e.target as HTMLElement).dataset.pageIndex);
              if (Number.isNaN(index)) continue;
              if (e.isIntersecting) void renderPage(index);
              else unloadPage(index);
            }
          },
          { root: scrollEl, rootMargin: "200% 0px" },
        );
        for (const wrapper of wrappers) if (wrapper) observer.observe(wrapper);

        if (initialPage > 0) {
          currentPage = Math.min(initialPage, pageCount - 1);
          await tick();
          scrollToPage(currentPage, "auto");
        }
      } catch (err) {
        if (!disposed) errorMessage = err instanceof Error ? err.message : String(err);
      }
    })();

    return () => {
      disposed = true;
      observer?.disconnect();
      observer = null;
      document.removeEventListener("selectionchange", handleSelectionSettle);
      if (selectionTimer) clearTimeout(selectionTimer);
      if (scrollRaf) cancelAnimationFrame(scrollRaf);
      try {
        pdf?.destroy?.();
      } catch {
        /* ignore */
      }
      pdf = null;
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
  {#snippet counter()}{pageCount > 0 ? `Page ${currentPage + 1} / ${pageCount}` : ""}{/snippet}

  {#snippet controls()}
    {#if bookmarksEnabled}
      {@const bookmarkLabel = pageBookmark ? "Remove bookmark" : "Bookmark this page"}
      <Button variant="outline" size="sm" type="button" onclick={toggleBookmark} aria-pressed={Boolean(pageBookmark)}
        class="aria-pressed:bg-accent aria-pressed:text-accent-foreground" aria-label={bookmarkLabel} title={bookmarkLabel}>
        {#if pageBookmark}<BookmarkCheck class="h-4 w-4" />{:else}<Bookmark class="h-4 w-4" />{/if}
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

    <Button variant="outline" size="sm"
      type="button"
      onclick={toggleSearch}
      aria-pressed={searchOpen}
      class="aria-pressed:bg-accent aria-pressed:text-accent-foreground"
      aria-label="Search document"
      title="Search"
    >
      <Search class="h-4 w-4" />
    </Button>

    <div class="flex items-center gap-1 border-l border-border-subtle pl-2">
      <Button variant="outline" size="sm"
        type="button"
        onclick={() => setFlow("scrolled")}
        aria-pressed={flow === "scrolled"}
        class="aria-pressed:bg-accent aria-pressed:text-accent-foreground"
        aria-label="Scrolled reading"
        title="Scrolled (selectable text)"
      >
        <Rows3 class="h-4 w-4" />
      </Button>
      <Button variant="outline" size="sm"
        type="button"
        onclick={() => setFlow("paged")}
        aria-pressed={flow === "paged"}
        class="aria-pressed:bg-accent aria-pressed:text-accent-foreground"
        aria-label="Paged reading"
        title="Paged (one page per view)"
      >
        <BookOpen class="h-4 w-4" />
      </Button>
    </div>

    {#if flow === "scrolled"}
      <div class="flex items-center gap-1 border-l border-border-subtle pl-2">
        <Button variant="outline" size="sm"
          type="button"
          onclick={zoomOut}
          disabled={scale <= MIN_SCALE}
          class="aria-pressed:bg-accent aria-pressed:text-accent-foreground"
          aria-label="Zoom out"
          title="Zoom out"
        >
          <Minus class="h-4 w-4" />
        </Button>
        <span class="min-w-[3ch] text-center font-mono text-[0.62rem] tabular-nums text-text-muted">{zoomPercent}%</span>
        <Button variant="outline" size="sm"
          type="button"
          onclick={zoomIn}
          disabled={scale >= MAX_SCALE}
          class="aria-pressed:bg-accent aria-pressed:text-accent-foreground"
          aria-label="Zoom in"
          title="Zoom in"
        >
          <Plus class="h-4 w-4" />
        </Button>
        <Button variant="outline" size="sm" type="button" onclick={fitWidth} class="aria-pressed:bg-accent aria-pressed:text-accent-foreground" aria-label="Fit width" title="Fit width">
          <span class="text-[0.62rem]">W</span>
        </Button>
        <Button variant="outline" size="sm" type="button" onclick={fitPage} class="aria-pressed:bg-accent aria-pressed:text-accent-foreground" aria-label="Fit page" title="Fit one page in view">
          <Maximize class="h-4 w-4" />
        </Button>
      </div>
    {/if}

    <div class="flex items-center gap-1 border-l border-border-subtle pl-2">
      {#if flow === "scrolled"}
        <Button variant="outline" size="sm"
          type="button"
          onclick={toggleGap}
          aria-pressed={gapless}
          class="aria-pressed:bg-accent aria-pressed:text-accent-foreground"
          aria-label="Toggle page gaps"
          title={gapless ? "Show page gaps" : "Remove page gaps"}
        >
          <StretchVertical class="h-4 w-4" />
        </Button>
      {/if}
      {#if downloadHref}
        <a
          href={downloadHref}
          download
          class={buttonVariants({ variant: "outline", size: "sm" })}
          aria-label="Download PDF"
          title="Download original PDF"
        >
          <Download class="h-4 w-4" />
        </a>
      {/if}
    </div>
  {/snippet}

  <!-- svelte-ignore a11y_no_static_element_interactions, a11y_click_events_have_key_events -->
  <div
    class="pdf-stage"
    class:paged={flow === "paged"}
    bind:this={scrollEl}
    onscroll={handleScroll}
    onclick={handleStageClick}
    onpointerdown={handleStagePointerDown}
    onpointermove={handleStagePointerMove}
    onpointerup={handleStagePointerEnd}
    onpointercancel={handleStagePointerEnd}
  >
    {#if ready}
      <div
        class="pdf-pages"
        class:gapless
        style={pinching ? `transform: scale(${pinchRatio}); transform-origin: center top` : ""}
      >
        {#each pageIndexes as index (index)}
          <div class="pdf-page" data-page-index={index} bind:this={wrappers[index]}>
            <div class="pdf-canvas-host" bind:this={canvasHosts[index]}></div>
            {#if annotationsEnabled}
              <PdfHighlightLayer page={index} annotations={annotationsForPage(index)} onTap={handleHighlightTap} />
            {/if}
          </div>
        {/each}
      </div>
    {/if}

    {#if annotationsEnabled}
      <ReaderSelectionMenu
        anchor={pendingSelection?.anchor ?? null}
        selectedText={pendingSelection?.draft.selectedText ?? ""}
        actions={selectionActions} onDismiss={clearSelection}
        sourceContext={pendingSelection && { bookId, locator: pendingSelection.draft.locator, textBefore: pendingSelection.draft.textBefore, textAfter: pendingSelection.draft.textAfter }}
      />
      <AnnotationEditor
        anchor={editingAnnotation ? editing?.anchor ?? null : null}
        color={editingAnnotation?.color ?? lastColor} note={editingAnnotation?.note ?? null}
        focusNote={editing?.focusNote ?? false}
        onChangeColor={(color) => {
          lastColor = color;
          rememberHighlightColor(color);
          if (editingAnnotation) onUpdateAnnotation?.({ ...editingAnnotation, color });
        }}
        onSaveNote={(note) => {
          if (editingAnnotation) onUpdateAnnotation?.({ ...editingAnnotation, note: note.trim() ? note : null });
          editing = null;
        }}
        onDelete={() => {
          if (editingAnnotation) onDeleteAnnotation?.(editingAnnotation);
          editing = null;
        }}
        onDismiss={() => (editing = null)}
      />
    {/if}

    {#if errorMessage}
      <div class="pdf-message">
        <AlertTriangle class="h-5 w-5" />
        <p>{errorMessage}</p>
      </div>
    {:else if !ready}
      <div class="pdf-message">
        <p>Opening document…</p>
      </div>
    {/if}
  </div>

  {#if ready && flow === "paged"}
    <!-- Fixed over the stage (siblings, not inside the scroller) so they don't scroll away. -->
    <Button variant="outline" size="sm"
      type="button"
      onclick={goPrev}
      data-reader-control
      class="absolute top-1/2 z-10 hidden size-11 -translate-y-1/2 sm:inline-flex left-3"
      aria-label="Previous page"
      title="Previous (←)"
    >
      <ChevronLeft class="h-6 w-6" />
    </Button>
    <Button variant="outline" size="sm"
      type="button"
      onclick={goNext}
      data-reader-control
      class="absolute top-1/2 z-10 hidden size-11 -translate-y-1/2 sm:inline-flex right-3"
      aria-label="Next page"
      title="Next (→)"
    >
      <ChevronRight class="h-6 w-6" />
    </Button>
  {/if}
</ReaderShell>

{#if ready}
  <PdfSearchPanel open={searchOpen} {pdf} onClose={() => (searchOpen = false)} onNavigate={(page) => scrollToPage(page)} />
{/if}

<ReaderContentsSheet open={tocOpen} onClose={() => tocOpen = false}
  bookmarks={bookmarksEnabled ? bookmarkItems : undefined} bookmarkCount={bookmarks?.length ?? 0}>
  {@render tocItems(toc, 0)}
</ReaderContentsSheet>

{#snippet bookmarkItems()}
  <ReaderBookmarkList bookmarks={bookmarks ?? []} onOpen={openBookmark}
    onRename={(bookmark, label) => onRenameBookmark?.(bookmark, label)}
    onDelete={(bookmark) => onDeleteBookmark?.(bookmark)} />
{/snippet}

{#snippet tocItems(items: TocEntry[], depth: number)}
  {#each items as entry (entry.label + (entry.pageIndex ?? ""))}
    <Button variant="ghost"
      class="h-auto w-full justify-start whitespace-normal py-2 text-left"
      style={`padding-left: ${0.85 + depth * 0.9}rem`}
      disabled={entry.pageIndex === null}
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
  .pdf-stage {
    position: absolute;
    inset: 0;
    overflow-y: auto;
    overflow-x: auto;
    /* M6 reader theme colours the chrome only (never the page canvases below): light/dark/sepia set
       --pdf-stage-background via JS; "auto" removes the var and falls back to the app's dark chrome. */
    background: var(--pdf-stage-background, #0b0c0f);
    overscroll-behavior: contain;
    /* Allow native one-finger scroll while we handle two-finger pinch ourselves. */
    touch-action: pan-x pan-y;
  }

  .pdf-pages {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 1rem;
    padding: max(3.5rem, env(safe-area-inset-top)) 0 4rem;
  }

  /* Stitch pages together with no gap for a continuous document feel. */
  .pdf-pages.gapless {
    gap: 0;
  }

  /* Paged mode: one page per view via scroll-snap; the gesture overlay drives turns. */
  .pdf-stage.paged {
    scroll-snap-type: y mandatory;
  }

  .pdf-stage.paged .pdf-page {
    scroll-snap-align: center;
    scroll-snap-stop: always;
  }

  /* In paged mode the text layer must not eat taps (it has no selection there); taps drive
     the comic-style tap zones instead. */
  .pdf-stage.paged :global(.pdf-text-layer),
  .pdf-stage.paged :global(.pdf-link-layer) {
    pointer-events: none;
  }

  .pdf-page {
    position: relative;
    background: #1a1c22;
    box-shadow: 0 0 24px rgba(0, 0, 0, 0.5);
  }

  /* Holds the imperative canvas/text/link layers, so Svelte can render the highlight overlay too. */
  .pdf-canvas-host {
    position: absolute;
    inset: 0;
  }

  :global(.pdf-canvas) {
    display: block;
  }

  /* pdf.js text layer: transparent, selectable text positioned over the canvas. */
  :global(.pdf-text-layer) {
    position: absolute;
    inset: 0;
    overflow: hidden;
    line-height: 1;
    text-size-adjust: none;
    forced-color-adjust: none;
    transform-origin: 0 0;
    z-index: 2;
  }

  :global(.pdf-text-layer span),
  :global(.pdf-text-layer br) {
    position: absolute;
    white-space: pre;
    color: transparent;
    cursor: text;
    transform-origin: 0% 0%;
  }

  :global(.pdf-text-layer ::selection) {
    background: rgba(199, 201, 204, 0.35);
  }

  /* Transparent clickable link overlay (internal page jumps + external URLs). */
  :global(.pdf-link-layer) {
    position: absolute;
    inset: 0;
    z-index: 3;
  }

  :global(.pdf-link-layer .pdf-link) {
    position: absolute;
    display: block;
    border-radius: 2px;
  }

  :global(.pdf-link-layer .pdf-link:hover) {
    background: rgba(199, 201, 204, 0.18);
    box-shadow: 0 0 0 1px rgba(199, 201, 204, 0.4);
  }

  .pdf-message {
    position: absolute;
    inset: 0;
    display: grid;
    place-items: center;
    gap: 0.6rem;
    color: var(--color-text-secondary);
    text-align: center;
    pointer-events: none;
  }

</style>

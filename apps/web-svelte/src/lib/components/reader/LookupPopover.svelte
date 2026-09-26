<script lang="ts">
  import { untrack } from "svelte";
  import { Button, Popover, Select } from "@prismedia/ui-svelte";
  import { ExternalLink } from "@lucide/svelte";
  import { measurableFromRect } from "$lib/reader/epub-selection";
  import { isDefinableSelection } from "$lib/reader/selection-lookup";
  import { TRANSLATE_LANGUAGE_OPTIONS, getStoredTargetLanguage, setStoredTargetLanguage } from "$lib/reader/translate-language";
  import { defineTerm, translateText, wikipediaSummary, type ReaderLookupOutcome } from "$lib/api/reader-lookups";
  import type {
    DictionaryDefinitionResponse,
    TranslateTextResponse,
    WikipediaSummaryResponse,
  } from "$lib/api/generated/model";
  import { VOCABULARY_STATUS } from "$lib/api/generated/codes";
  import { saveVocabularyEntry } from "$lib/api/vocabulary";
  import { createUuid } from "$lib/utils/uuid";
  import { BookMarked, Check } from "@lucide/svelte";
  import { buildSourceSentence, type LookupSourceContext } from "$lib/reader/reader-lookup-actions";

  type LookupMode = "define" | "translate" | "wikipedia";

  interface Props {
    /** Viewport rect of the selection this popover is anchored to; null hides it. */
    anchor: { left: number; top: number; width: number; height: number } | null;
    /** The selected text the popover looks up. */
    term: string;
    /** Which view opens first. */
    initialMode: LookupMode;
    onDismiss: () => void;
    /** Where this selection lives in the book, saved with Add to Vocabulary. */
    sourceContext?: LookupSourceContext | null;
  }

  let { anchor, term, initialMode, onDismiss, sourceContext = null }: Props = $props();

  // `mode` only takes its initial value from `initialMode` on mount; after that it is switched by
  // the tab buttons or reset by the effect below when the popover is reopened for a new selection.
  let mode = $state<LookupMode>(untrack(() => initialMode));
  let targetLanguage = $state(getStoredTargetLanguage());
  const customAnchor = $derived(anchor ? measurableFromRect(anchor) : null);
  const canDefine = $derived(isDefinableSelection(term));

  // One in-flight/loaded result per mode, keyed so re-opening the same term doesn't refetch.
  let loaded = $state<{ term: string; mode: LookupMode } | null>(null);
  let loading = $state(false);
  let defineResult = $state<ReaderLookupOutcome<DictionaryDefinitionResponse> | null>(null);
  let vocabularyState = $state<"idle" | "saving" | "added">("idle");
  let translateResult = $state<ReaderLookupOutcome<TranslateTextResponse> | null>(null);
  let wikipediaResult = $state<ReaderLookupOutcome<WikipediaSummaryResponse> | null>(null);

  $effect(() => {
    if (!anchor) return;
    mode = initialMode;
    defineResult = null;
    translateResult = null;
    wikipediaResult = null;
    loaded = null;
    vocabularyState = "idle";
  });

  /**
   * Saves the current term to the reader's vocabulary list: word, first definition, book id,
   * CFI locator, and the surrounding sentence (from the selection's text-quote before/after),
   * so the saved entry can jump back to where it was found.
   */
  async function addToVocabulary() {
    if (vocabularyState !== "idle" || defineResult?.status !== "ok") return;
    vocabularyState = "saving";
    const firstDefinition = defineResult.data.entries[0]?.definitions[0]?.text ?? null;
    try {
      await saveVocabularyEntry(createUuid(), {
        word: term,
        language: "en",
        definition: firstDefinition,
        bookId: sourceContext?.bookId ?? null,
        sourceLocator: sourceContext?.locator ?? null,
        sourceSentence: buildSourceSentence(term, sourceContext),
        status: VOCABULARY_STATUS.new,
        updatedAt: new Date().toISOString(),
      });
      vocabularyState = "added";
    } catch {
      vocabularyState = "idle";
    }
  }

  $effect(() => {
    if (!anchor || !term) return;
    void runLookup(mode);
  });

  async function runLookup(next: LookupMode) {
    mode = next;
    if (loaded?.term === term && loaded.mode === next) return;
    loading = true;
    try {
      if (next === "define") defineResult = await defineTerm(term, targetLanguageForDefine());
      else if (next === "translate") translateResult = await translateText(term, targetLanguage, undefined);
      else wikipediaResult = await wikipediaSummary(term, "en");
      loaded = { term, mode: next };
    } finally {
      loading = false;
    }
  }

  // Define always looks up in the reader's content language proxy (English default); the reader
  // does not yet track the book's own language (M6), so this matches the server default.
  function targetLanguageForDefine() {
    return "en";
  }

  function changeTargetLanguage(next: string) {
    targetLanguage = next;
    setStoredTargetLanguage(next);
    translateResult = null;
    loaded = null;
    void runLookup("translate");
  }
</script>

<Popover.Root open={anchor !== null} onOpenChange={(next) => { if (!next) onDismiss(); }}>
  {#if customAnchor}
    <Popover.Content
      {customAnchor}
      portalProps={{ disabled: true }}
      side="bottom"
      sideOffset={8}
      class="w-80 max-h-96 overflow-y-auto gap-2 p-3"
      aria-label="Word lookup"
    >
      <div class="flex items-center gap-1 border-b border-border pb-2" role="tablist" aria-label="Lookup type">
        {#if canDefine}
          <Button variant={mode === "define" ? "secondary" : "ghost"} size="sm" type="button" onclick={() => runLookup("define")}>
            Define
          </Button>
        {/if}
        <Button variant={mode === "translate" ? "secondary" : "ghost"} size="sm" type="button" onclick={() => runLookup("translate")}>
          Translate
        </Button>
        <Button variant={mode === "wikipedia" ? "secondary" : "ghost"} size="sm" type="button" onclick={() => runLookup("wikipedia")}>
          Wikipedia
        </Button>
      </div>

      <p class="text-label font-medium text-foreground">{term}</p>

      {#if loading}
        <p class="text-label text-muted-foreground">Looking up…</p>
      {:else if mode === "define"}
        {#if defineResult?.status === "ok"}
          {#each defineResult.data.entries as entry (entry.partOfSpeech ?? entry.definitions[0]?.text)}
            {#if entry.partOfSpeech}
              <p class="text-label font-medium italic text-muted-foreground">{entry.partOfSpeech}</p>
            {/if}
            <ol class="list-decimal space-y-1 pl-5 text-label">
              {#each entry.definitions as definition (definition.text)}
                <li>
                  {definition.text}
                  {#each definition.examples as example (example)}
                    <p class="text-label italic text-muted-foreground">“{example}”</p>
                  {/each}
                </li>
              {/each}
            </ol>
          {/each}
          <a
            href={defineResult.data.sourceUrl}
            target="_blank"
            rel="noreferrer"
            class="inline-flex items-center gap-1 text-label text-muted-foreground hover:text-foreground"
          >
            {defineResult.data.attribution}
            <ExternalLink class="size-3" />
          </a>
          <Button variant={vocabularyState === "added" ? "secondary" : "outline"} size="sm" type="button"
            disabled={vocabularyState !== "idle"} onclick={addToVocabulary}>
            {#if vocabularyState === "added"}
              <Check class="size-3.5" /> In vocabulary
            {:else}
              <BookMarked class="size-3.5" /> Add to Vocabulary
            {/if}
          </Button>
        {:else if defineResult?.status === "not-found"}
          <p class="text-label text-muted-foreground">No definition found for "{term}".</p>
        {:else if defineResult?.status}
          <p class="text-label text-destructive">{defineResult.message}</p>
        {/if}
      {:else if mode === "translate"}
        <div class="w-40">
          <Select
            options={TRANSLATE_LANGUAGE_OPTIONS.map((option) => ({ value: option.value, label: option.label }))}
            value={targetLanguage}
            size="sm"
            ariaLabel="Translate to"
            onchange={changeTargetLanguage}
          />
        </div>
        {#if translateResult?.status === "ok"}
          <p class="text-label">{translateResult.data.translatedText}</p>
        {:else if translateResult?.status}
          <p class="text-label text-destructive">
            {translateResult.status === "invalid" || translateResult.status === "unavailable"
              ? translateResult.message
              : "No translation found."}
          </p>
        {/if}
      {:else if mode === "wikipedia"}
        {#if wikipediaResult?.status === "ok"}
          <p class="text-label font-medium">{wikipediaResult.data.title}</p>
          <p class="text-label">{wikipediaResult.data.extract}</p>
          <a
            href={wikipediaResult.data.url}
            target="_blank"
            rel="noreferrer"
            class="inline-flex items-center gap-1 text-label text-muted-foreground hover:text-foreground"
          >
            Open article
            <ExternalLink class="size-3" />
          </a>
        {:else if wikipediaResult?.status === "not-found"}
          <p class="text-label text-muted-foreground">No Wikipedia article found for "{term}".</p>
        {:else if wikipediaResult?.status}
          <p class="text-label text-destructive">{wikipediaResult.message}</p>
        {/if}
      {/if}
    </Popover.Content>
  {/if}
</Popover.Root>

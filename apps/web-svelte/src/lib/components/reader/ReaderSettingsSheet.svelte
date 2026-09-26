<script lang="ts">
  import { Button, Select, Sheet, Slider, Toggle } from "@prismedia/ui-svelte";
  import { X } from "@lucide/svelte";
  import { READER_FONT_FAMILY, READER_THEME } from "$lib/api/generated/codes";
  import { TRANSLATE_LANGUAGE_OPTIONS } from "$lib/reader/translate-language";
  import type { ReaderPreferences } from "$lib/reader/reader-preferences-store.svelte";

  interface Props {
    open: boolean;
    preferences: ReaderPreferences;
    /** Fixed-layout books (PDF, or fixed-layout EPUB) ignore typography settings. */
    isFixedLayout?: boolean;
    onClose: () => void;
    onChange: (patch: Partial<ReaderPreferences>) => void;
  }

  let { open, preferences, isFixedLayout = false, onClose, onChange }: Props = $props();

  const FONT_FAMILY_OPTIONS = [
    { value: READER_FONT_FAMILY.publisher, label: "Publisher default" },
    { value: READER_FONT_FAMILY.serif, label: "Serif" },
    { value: READER_FONT_FAMILY.sans, label: "Sans" },
    { value: READER_FONT_FAMILY.monospace, label: "Monospace" },
  ];
  const THEME_OPTIONS = [
    { value: READER_THEME.auto, label: "Match app" },
    { value: READER_THEME.light, label: "Light" },
    { value: READER_THEME.dark, label: "Dark" },
    { value: READER_THEME.sepia, label: "Sepia" },
  ];

  let fontSizeSlider = $state(100);
  let lineHeightSlider = $state(1.5);
  let marginSlider = $state(1);
  let widthSlider = $state(900);

  $effect(() => {
    fontSizeSlider = preferences.fontSizePercent;
    lineHeightSlider = preferences.lineHeight;
    marginSlider = preferences.marginStep;
    widthSlider = preferences.maxContentWidth;
  });
</script>

<Sheet.Root {open} onOpenChange={(next) => { if (!next) onClose(); }}>
  {#if open}
    <Sheet.Content side="right" showCloseButton={false} class="w-[min(22rem,86vw)] gap-0 sm:max-w-[22rem]">
      <Sheet.Header class="flex-row items-center justify-between border-b px-4 pt-[max(0.75rem,env(safe-area-inset-top))] pb-3">
        <Sheet.Title>Reading settings</Sheet.Title>
        <Button variant="ghost" size="icon" aria-label="Close reading settings" onclick={onClose}><X /></Button>
      </Sheet.Header>

      <div class="flex min-h-0 flex-1 flex-col gap-4 overflow-y-auto p-4 pb-[max(1rem,env(safe-area-inset-bottom))]">
        {#if isFixedLayout}
          <p class="text-label text-muted-foreground">
            This book has a fixed page layout; typography settings do not apply. Theme still does.
          </p>
        {/if}

        <label class="flex flex-col gap-1">
          <span class="text-label font-medium text-foreground">Theme</span>
          <Select options={THEME_OPTIONS} value={preferences.theme} ariaLabel="Reader theme"
            onchange={(value) => onChange({ theme: value as ReaderPreferences["theme"] })} />
        </label>

        {#if !isFixedLayout}
          <label class="flex flex-col gap-1">
            <span class="text-label font-medium text-foreground">Font</span>
            <Select options={FONT_FAMILY_OPTIONS} value={preferences.fontFamily} ariaLabel="Reader font family"
              onchange={(value) => onChange({ fontFamily: value as ReaderPreferences["fontFamily"] })} />
          </label>

          <label class="flex flex-col gap-1">
            <span class="flex items-center justify-between text-label font-medium text-foreground">
              Font size <span class="text-muted-foreground">{preferences.fontSizePercent}%</span>
            </span>
            <Slider type="single" bind:value={fontSizeSlider} min={80} max={200} step={5} thumbLabel="Font size"
              onValueCommit={(value) => onChange({ fontSizePercent: value })} />
          </label>

          <label class="flex flex-col gap-1">
            <span class="flex items-center justify-between text-label font-medium text-foreground">
              Line height <span class="text-muted-foreground">{preferences.lineHeight.toFixed(1)}</span>
            </span>
            <Slider type="single" bind:value={lineHeightSlider} min={1.2} max={2} step={0.1} thumbLabel="Line height"
              onValueCommit={(value) => onChange({ lineHeight: value })} />
          </label>

          <label class="flex flex-col gap-1">
            <span class="flex items-center justify-between text-label font-medium text-foreground">
              Margins <span class="text-muted-foreground">{preferences.marginStep}</span>
            </span>
            <Slider type="single" bind:value={marginSlider} min={0} max={4} step={1} thumbLabel="Margins"
              onValueCommit={(value) => onChange({ marginStep: value })} />
          </label>

          <label class="flex flex-col gap-1">
            <span class="flex items-center justify-between text-label font-medium text-foreground">
              Max reading width <span class="text-muted-foreground">{preferences.maxContentWidth}px</span>
            </span>
            <Slider type="single" bind:value={widthSlider} min={320} max={1200} step={20} thumbLabel="Maximum reading width"
              onValueCommit={(value) => onChange({ maxContentWidth: value })} />
          </label>

          <label class="flex items-center justify-between gap-2 text-label font-medium text-foreground">
            Continuous scroll
            <Toggle checked={preferences.flow === "scrolled"} ariaLabel="Continuous scroll flow"
              onchange={(checked) => onChange({ flow: checked ? "scrolled" : "paged" })} />
          </label>
        {/if}

        <label class="flex flex-col gap-1">
          <span class="text-label font-medium text-foreground">Translate to</span>
          <Select
            options={TRANSLATE_LANGUAGE_OPTIONS.map((option) => ({ value: option.value, label: option.label }))}
            value={preferences.translationTargetLanguage ?? "en"}
            ariaLabel="Default translate-to language"
            onchange={(value) => onChange({ translationTargetLanguage: value })} />
        </label>
      </div>
    </Sheet.Content>
  {/if}
</Sheet.Root>

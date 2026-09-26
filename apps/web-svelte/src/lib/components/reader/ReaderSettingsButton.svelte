<script lang="ts">
  import { Button } from "@prismedia/ui-svelte";
  import { Settings2 } from "@lucide/svelte";
  import ReaderSettingsSheet from "./ReaderSettingsSheet.svelte";
  import type { ReaderPreferences } from "$lib/reader/reader-preferences-store.svelte";

  interface Props {
    preferences: ReaderPreferences;
    isFixedLayout?: boolean;
    onChange: (patch: Partial<ReaderPreferences>) => void;
  }

  let { preferences, isFixedLayout = false, onChange }: Props = $props();
  let open = $state(false);
</script>

<Button variant="outline" size="sm" type="button" onclick={() => (open = true)}
  aria-label="Reading settings" title="Reading settings">
  <Settings2 class="h-4 w-4" />
</Button>

<ReaderSettingsSheet {open} {preferences} {isFixedLayout} onClose={() => (open = false)} {onChange} />

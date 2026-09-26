<script lang="ts">
  import type { ComponentProps } from "svelte";
  import { provideNsfw } from "$lib/nsfw/store.svelte";
  import EntityGrid from "./EntityGrid.svelte";

  const props: ComponentProps<typeof EntityGrid> = $props();

  provideNsfw(() => ({ initialMode: "show", allowed: true }));
</script>

<EntityGrid {...props} />

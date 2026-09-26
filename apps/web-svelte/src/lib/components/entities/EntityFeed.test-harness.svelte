<script lang="ts">
  import type { ComponentProps } from "svelte";
  import { provideNsfw } from "$lib/nsfw/store.svelte";
  import EntityFeed from "./EntityFeed.svelte";

  const props: ComponentProps<typeof EntityFeed> = $props();

  provideNsfw(() => ({ initialMode: "show", allowed: true }));
</script>

<EntityFeed {...props} />

<script lang="ts">
  import { page } from "$app/state";
  import VocabularyLibrary from "$lib/components/vocabulary/VocabularyLibrary.svelte";
</script>

<svelte:head>
  <title>Vocabulary · Prismedia</title>
</svelte:head>

<VocabularyLibrary initialSearch={page.url.searchParams.get("q") ?? ""} />

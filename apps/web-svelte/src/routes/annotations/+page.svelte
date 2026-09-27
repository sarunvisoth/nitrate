<script lang="ts">
  import { page } from "$app/state";
  import AnnotationLibrary from "$lib/components/annotations/AnnotationLibrary.svelte";
</script>

<svelte:head>
  <title>Annotations · Prismedia</title>
</svelte:head>

<AnnotationLibrary initialSearch={page.url.searchParams.get("q") ?? ""} />

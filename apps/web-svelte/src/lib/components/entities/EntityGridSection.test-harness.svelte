<script lang="ts">
  import EntityGridSection from "./EntityGridSection.svelte";

  interface Props {
    prefsKey?: string;
  }

  let { prefsKey = "section-test" }: Props = $props();
</script>

<EntityGridSection title="Sub Galleries" count={3} {prefsKey}>
  <div data-testid="section-grid">Grid content</div>
</EntityGridSection>

<script lang="ts">
  import { Button } from "@prismedia/ui-svelte";
  import ReaderContentsSheet from "./ReaderContentsSheet.svelte";
</script>

<ReaderContentsSheet open={true} onClose={() => {}} bookmarkCount={1}>
  <Button>Chapter one</Button>
  {#snippet bookmarks()}
    <Button>Bookmark one</Button>
  {/snippet}
</ReaderContentsSheet>

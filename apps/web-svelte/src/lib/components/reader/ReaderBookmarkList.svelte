<script lang="ts">
  import { Button, TextInput } from "@prismedia/ui-svelte";
  import { Check, Pencil, Trash2 } from "@lucide/svelte";
  import type { BookBookmark } from "$lib/api/generated/model";

  interface Props {
    bookmarks: readonly BookBookmark[];
    onOpen: (bookmark: BookBookmark) => void;
    onRename: (bookmark: BookBookmark, label: string) => void;
    onDelete: (bookmark: BookBookmark) => void;
  }

  let { bookmarks, onOpen, onRename, onDelete }: Props = $props();
  let renaming = $state<{ id: string; label: string } | null>(null);

  function save(bookmark: BookBookmark) {
    if (!renaming) return;
    onRename(bookmark, renaming.label);
    renaming = null;
  }
</script>

{#if bookmarks.length === 0}
  <p class="px-3 py-4 text-sm text-text-muted">No bookmarks yet. Use the bookmark button in the top bar to mark a page.</p>
{:else}
  <ul class="flex flex-col gap-1">
    {#each bookmarks as bookmark (bookmark.id)}
      <li class="flex items-center gap-1">
        {#if renaming?.id === bookmark.id}
          <TextInput
            size="sm"
            class="min-w-0 flex-1"
            aria-label="Bookmark label"
            placeholder={bookmark.chapterLabel ?? "Bookmark"}
            bind:value={renaming.label}
            onkeydown={(event: KeyboardEvent) => { if (event.key === "Enter") save(bookmark); }}
          />
          <Button variant="ghost" size="icon-sm" type="button" aria-label="Save label" onclick={() => save(bookmark)}>
            <Check class="h-4 w-4" />
          </Button>
        {:else}
          <Button variant="ghost"
            class="h-auto min-w-0 flex-1 justify-start whitespace-normal py-2 text-left"
            onclick={() => onOpen(bookmark)}
          >
            <span class="flex min-w-0 flex-col">
              <span>{bookmark.label ?? bookmark.chapterLabel ?? "Bookmark"}</span>
              <span class="font-mono text-[0.6rem] uppercase tracking-[0.14em] text-text-muted">
                {Math.round(Number(bookmark.position) * 100)}%{bookmark.label && bookmark.chapterLabel ? ` · ${bookmark.chapterLabel}` : ""}
              </span>
            </span>
          </Button>
          <Button variant="ghost" size="icon-sm" type="button" aria-label="Rename bookmark"
            onclick={() => (renaming = { id: bookmark.id, label: bookmark.label ?? "" })}
          >
            <Pencil class="h-4 w-4" />
          </Button>
        {/if}
        <Button variant="ghost" size="icon-sm" type="button" aria-label="Delete bookmark" onclick={() => onDelete(bookmark)}>
          <Trash2 class="h-4 w-4" />
        </Button>
      </li>
    {/each}
  </ul>
{/if}

<script lang="ts">
  import { cn } from "@prismedia/ui-svelte";
  import { initials } from "./initials";

  interface Props {
    displayName: string | null | undefined;
    username?: string | null;
    size?: "sm" | "md" | "lg";
    class?: string;
  }

  let { displayName, username, size = "sm", class: className }: Props = $props();

  const sizeClasses = {
    sm: "size-6 text-[0.6rem]",
    md: "size-8 text-xs",
    lg: "size-14 text-lg",
  } as const;
</script>

<!-- A disc is an avatar, not a container: rounded-full is intentional here. -->
<span
  class={cn(
    "flex shrink-0 items-center justify-center rounded-full border border-border-default bg-surface-3 font-heading font-semibold tracking-wide text-text-secondary select-none",
    sizeClasses[size],
    className,
  )}
  aria-hidden="true"
>
  {initials(displayName, username)}
</span>

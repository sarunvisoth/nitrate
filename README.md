# Nitrate

A private, self-hosted media library with a serious reading experience, adapted to run natively on a
Windows PC with an AMD GPU.

Nitrate is a **personal, non-commercial fork** of [Prismedia](https://github.com/pauljoda/Prismedia) by
Paul Davis. It starts from Prismedia commit `627958a` (v3.7.2) and is never synced with upstream.
Prismedia is licensed under
[CC BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/); Nitrate keeps that licence (see
[LICENSE](LICENSE)). Nitrate is not affiliated with or endorsed by Prismedia or its author.
Everything not listed under "What Nitrate adds" is Prismedia's work, including the media library
architecture, the video, music, audiobook, comic and image features, and the original design.
The upstream project's own README, documentation and apps are at
[github.com/pauljoda/Prismedia](https://github.com/pauljoda/Prismedia).

## What Nitrate adds

The changes since the Prismedia snapshot are about 260 files and 38,000 added lines, all in this
repository's git history (`git log 627958a..HEAD`). Each milestone has a plan in
[`docs/nitrate/plans/`](docs/nitrate/plans/) and is tracked in [`docs/nitrate/ROADMAP.md`](docs/nitrate/ROADMAP.md).

**A book reader with annotations**
- Highlights in five colours, notes and bookmarks on EPUB and PDF (page-anchored for PDF), synced across devices.
- An `/annotations` dashboard with search, filtering by book and colour, and Markdown / JSON / CSV export.
- Re-anchoring of annotations when a book's text changes.
- Selection menu with Define, Translate, Wikipedia and Search Book / Web, behind swappable
  `DictionaryProvider` and `TranslationProvider` interfaces.
- Per-user reader settings (fonts, spacing, themes, scroll or paginated) and a vocabulary list that links
  back to the source book and location.
- A unified search endpoint covering books, annotations and vocabulary, plus a mobile and tablet layout pass
  (safe-area insets, touch targets).

**Windows-native hosting**
- Runs without Docker, because Docker and WSL2 cannot use AMD's AMF hardware encoder; includes an AMD AMF
  encoder profile and a [Windows install guide](docs/nitrate/WINDOWS_HOST.md).
- Scheduled-Task autostart scripts for the API and worker (`scripts/windows/`).
- A folder picker that browses Windows drives, and music import that uses folder artwork.
- A [remote access guide](docs/nitrate/REMOTE_ACCESS.md) (Tailscale, Cloudflare Tunnel, Wake-on-LAN) and an API
  fix so forwarded headers are honoured only from a trusted loopback proxy.

**Documentation and tests**
- An [18-question architecture analysis](docs/nitrate/ARCHITECTURE_ANALYSIS.md) of the upstream codebase,
  and a [third-party licence inventory](docs/THIRD_PARTY_LICENSES.md).
- Playwright end-to-end specs for the reader, annotations, lookups, settings, global search and a mobile smoke test (`e2e/`).

## How it was built

Nitrate was built with [Claude Code](https://claude.com/claude-code). I set the direction, decisions and
roadmap (drafted together with Claude), ran each milestone as a Claude Code session, merged the resulting
pull requests, and tested on the target Windows machine, which found and fixed real problems
(for example the autostart task failing under the SYSTEM account). Most of the code in the commit history
was written by Claude Code, so many commits are authored or co-authored by it.
Several features are built and covered by tests but are still marked "awaiting owner verification" in the
roadmap, meaning I have not yet confirmed them on real hardware.

## Status

Personal project. It is not a release: there are no packaged versions, no
published container images and no support. The Windows guide has been run on one machine only.

## Running it

Nitrate keeps Prismedia's .NET backend, SvelteKit web app and Postgres database.

- Native Windows: follow [`docs/nitrate/WINDOWS_HOST.md`](docs/nitrate/WINDOWS_HOST.md).
- Development setup: the repository flake (`nix develop`) or the manual prerequisites in
  [`docs/nix-development.md`](docs/nix-development.md) and [`docs/architecture.md`](docs/architecture.md).
  Install dependencies with `pnpm install`, then run `pnpm dev` for the web app, and `pnpm test` for the unit tests.

Internal identifiers, package names and some UI strings still say "Prismedia", because the fork was not
rebranded throughout.

## Licence and attribution

Licensed under [CC BY-NC-SA 4.0](LICENSE): free to share and adapt with attribution, for non-commercial use,
under the same licence. Original work © 2025 Paul Davis. Changes by sarunvisoth (with Claude Code) are described above and
recorded in the git history. Third-party dependencies are listed in
[`docs/THIRD_PARTY_LICENSES.md`](docs/THIRD_PARTY_LICENSES.md).

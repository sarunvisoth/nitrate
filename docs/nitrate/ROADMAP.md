# Nitrate Roadmap

Spec: [VISION.md](VISION.md). Each milestone gets its own detailed plan in [plans/](plans/),
written **only after the previous milestone lands**, because later plans depend on what earlier
milestones discover. Update the Status column when a milestone completes.

Legend for who does the work: **Cloud** = a Claude cloud session can do it end to end.
**Owner** = needs the Windows PC, a real device, or a judgement call from the owner.

| # | Milestone | Who | Plan | Status |
|---|-----------|-----|------|--------|
| M0 | Import Prismedia snapshot, repo hygiene, Nitrate agent instructions | Cloud | [foundation](plans/2026-09-26-foundation.md) | Done |
| M1 | `docs/nitrate/ARCHITECTURE_ANALYSIS.md` (18 questions) + `THIRD_PARTY_LICENSES.md` | Cloud | [foundation](plans/2026-09-26-foundation.md) | Done |
| M2 | Windows-native host: AMD AMF encoder profile + Windows install guide | Cloud builds, Owner verifies | [foundation](plans/2026-09-26-foundation.md) | Built; awaiting owner verification |
| M3 | **Reader slice 1:** annotation model + API, text selection, highlights (5 colours), notes, cross-device sync | Cloud; Owner reads with it | [m3-reader-slice-1](plans/2026-09-26-m3-reader-slice-1.md) | Plan approved (with progress catch-up) — ready to execute |
| M4 | Bookmarks, `/annotations` dashboard (search, filter by book/colour), Markdown/JSON/CSV export | Cloud | — | Not started |
| M5 | `DictionaryProvider` + Define popup, `TranslationProvider` + Translate, Wikipedia / Search Book / Search Web | Cloud | — | Not started |
| M6 | Per-user reader settings (fonts, spacing, themes, scroll/paginated), vocabulary list | Cloud | — | Not started |
| M7 | PDF annotations (page-anchored), PDF search/define/translate | Cloud | — | Not started |
| M8 | Mobile/tablet layout pass, unified global search | Cloud; Owner checks devices | — | Not started |
| M9 | Autostart on Windows boot, Tailscale/WireGuard remote access, Wake-on-LAN | Owner-heavy | — | Not started |
| M10 | Native client investigation (Android/macOS/Windows) | Cloud research, Owner decides | — | Not started |

## Rules for every milestone

- Existing media features (video, music, audiobooks, comics, images) must keep working.
  Run the backend and web test suites before finishing a milestone.
- Existing HTTP endpoints stay backward compatible (the closed-source Prismedia App Store apps use them).
  New reader endpoints are additive.
- Each milestone ends with: tests green, docs updated, this table updated, pushed to `main`.

# Nitrate — Vision & Spec

This is the owner's product vision. It is the spec every Nitrate plan argues from.
Decisions recorded under **Decisions log** override anything below them.

## Decisions log (2026-09-26)

1. **Base:** Nitrate is a one-time snapshot fork of [Prismedia](https://github.com/pauljoda/Prismedia)
   at commit `627958a` (2026-09-18, v3.7.2). **No upstream syncing.** We own the code from here and
   may edit any Prismedia file directly; no need to isolate changes for merge-friendliness.
2. **License:** Prismedia is CC BY-NC-SA 4.0. Nitrate is private, personal, non-commercial. Keep
   `LICENSE` and credit Prismedia in the README. Never copy code from Readest (AGPL) or BookOrbit;
   they are UX/architecture references only. foliate-js (MIT) and PDF.js (Apache-2.0) are fine.
3. **All-in-one:** one server, one account, one library. Do not propose splitting media types
   across separate servers (e.g. Jellyfin + something).
4. **Host:** the owner's Windows PC with an **AMD CPU and AMD GPU**, running only while the PC is on.
   Prefer a **native Windows** install over Docker (Docker/WSL2 cannot use AMD's AMF encoder and
   reserves RAM). Prismedia has no AMF profile today — we add one.
5. **Development:** happens on macOS and in Claude cloud sessions; Windows is the deployment target.
   Anything that needs the Windows PC or a real device is a **owner-verified** step.
6. **Apple apps:** Prismedia's iPhone/iPad/Apple TV apps are closed-source App Store apps, not in this
   repo. Existing HTTP endpoints they use must stay backward compatible so they keep working.
7. **Delivery:** reader work ships as thin vertical slices the owner can actually read with,
   not broad horizontal layers.

---

# Project Goal

Build a self-hosted personal media platform by forking **Prismedia** and improving it into a serious all-in-one server for:

- Movies
- TV shows
- Music
- Audiobooks
- EPUB books
- PDFs
- Comics / manga
- Images
- General personal media

The server will run on my **Windows PC** and expose the library to my other devices whenever the PC is powered on.

The main reason for using Prismedia is that it already provides a strong unified base for media scanning, metadata, streaming, transcoding, accounts, progress tracking, and multiple media types.

The main area that needs major improvement is **reading**.

---

# High-Level Architecture

```text
Windows PC
└── Prismedia Server
    ├── Database
    ├── Authentication
    ├── Media scanner
    ├── Metadata
    ├── FFmpeg transcoding
    ├── User progress
    ├── Collections
    │
    ├── Movies / TV
    ├── Music
    ├── Audiobooks
    ├── Books
    ├── PDFs
    ├── Comics
    └── Images
             │
             ▼
        Unified API
             │
      ┌──────┼──────┐
      ▼      ▼      ▼
     Web    iPad   iPhone
      │
      ├── Mac browser/PWA initially
      └── Android browser/PWA initially
```

Later, investigate a cross-platform native client for macOS, Android, Windows, iOS / iPadOS.
Prefer one shared codebase if practical.

---

# Phase 1 — Get Prismedia Running Locally

1. Clone the Prismedia repository.
2. Understand the repository structure.
3. Identify: frontend, backend, database, API, authentication, book reader, media player, FFmpeg integration, library scanning.
4. Run it locally on Windows.
5. Create test libraries:

```text
D:\HomeServer\
├── Movies\
├── TV\
├── Music\
├── Audiobooks\
├── Books\
├── Comics\
└── Photos\
```

6. Verify access from: Windows browser, Mac, iPad, Android phone.

Do not modify major architecture yet. Document how Prismedia currently works before changing it.

---

# Phase 2 — Understand the Existing Reader

Investigate the current EPUB/PDF implementation. Prismedia appears to use `foliate-js` for EPUB and `PDF.js` for PDF. Confirm this in the repository.

Map out:

```text
Book file
   ↓
Reader renderer
   ↓
Reading position
   ↓
Database/API
   ↓
User account
```

Determine what Prismedia already stores for: reading progress, current EPUB location, bookmarks, annotations, user preferences.

Do not assume features exist. Verify from the source.

---

# Phase 3 — Build a Serious EPUB Reader

The reading experience should eventually be closer to Kindle, Readest, BookOrbit, KOReader rather than a simple browser EPUB viewer.

Use **Readest** and **BookOrbit** as UX and architecture references. Do not blindly copy their code because of licensing differences. Use compatible libraries such as `foliate-js` where appropriate.

---

# Reader Requirements

## Text Selection Menu

When a user selects text:

```text
┌──────────────────────────────┐
│ Define                       │
│ Translate                    │
│ Highlight                    │
│ Add Note                     │
│ Search Book                  │
│ Wikipedia                    │
│ Copy                         │
└──────────────────────────────┘
```

## Highlights

Colors: yellow, blue, green, red, purple. Eventually styles: highlight, underline, strikethrough, squiggly underline.

Each annotation should store something equivalent to:

```ts
Annotation {
    id
    userId
    bookId

    locator
    selectedText
    surroundingText

    type
    color

    note

    createdAt
    updatedAt
}
```

For EPUB, use a stable location system such as EPUB CFI or whatever locator mechanism foliate-js recommends.

Annotations must survive: server restart, browser refresh, device changes, book reopening.

## Notes

A highlight may optionally have a note.

```text
Highlighted text:
"Diffusion models gradually remove noise..."

My note:
Compare this with rectified flow formulation.
```

Users should be able to: edit notes, delete notes, search notes, filter by book, filter by highlight color.

## Bookmarks

Bookmarks are separate from highlights. Each bookmark contains: book, location, chapter, createdAt, optional label. Users can jump directly back to them.

## Annotation Dashboard

A global page `/annotations`:

```text
All Annotations

Books
├── Dune
│   ├── 19 highlights
│   └── 5 notes
│
├── Deep Learning
│   ├── 47 highlights
│   └── 16 notes
│
└── Paper Collection
```

Allow searching across annotation text. Eventually export: Markdown, JSON, CSV.

## Dictionary System

Dictionary lookup is one of the most important features. Selecting a word allows **Define**.
Initial implementation may use an online dictionary API. Later support local dictionaries.

```text
DictionaryProvider

├── OnlineDictionaryProvider
├── WiktionaryProvider
├── LocalStarDictProvider
├── LocalMDictProvider
└── CustomDictionaryProvider
```

UI example:

```text
ubiquitous

adjective

present, appearing, or found everywhere

Synonyms:
pervasive
omnipresent

Japanese:
至る所にある

[Wikipedia]
[Translate]
[Add to Vocabulary]
```

## Translation

Translate selected word / sentence / paragraph. Provider-agnostic via a `TranslationProvider`
abstraction; do not couple the reader to one commercial API.

## Wikipedia / Search

For selected text: Wikipedia, Search Book (current book), Search Web. Eventually library-wide full-text search.

## Vocabulary

After dictionary lookup works: `[Add to Vocabulary]` stores

```ts
VocabularyEntry {
    userId
    word
    definition
    sourceBook
    sourceLocation
    sourceSentence
    createdAt
    status
}
```

Statuses: New, Learning, Familiar, Known. Later possibly spaced repetition.

## Reader Settings

Font family, font size, line height, margins, page width, light/dark/sepia themes, scrolling and paginated modes. Stored per user.

## Progress Synchronization

Reading progress syncs across devices (Windows → iPad → Android resume at the same place). Same for highlights, notes, bookmarks, and reading settings where appropriate.

## PDF Reader

Use the existing PDF.js foundation. Add highlights, notes, bookmarks, search, dictionary lookup, translation.
Do not assume EPUB locators can be reused for PDFs — use PDF-specific coordinates/page anchors.

---

# Media Features (preserve — do not break)

- **Video:** direct play, FFmpeg transcoding, subtitles, playback progress, metadata
- **Music:** albums, artists, playlists, playback progress where relevant
- **Audiobooks:** resume position, chapter navigation, playback speed
- **Comics:** page navigation, reading progress, manga direction if available

# Unified Search (eventually)

One global search across Books, Movies, Audiobooks, Annotations, Music.

# Device Strategy

First version: server + responsive web UI for Mac, iPad, Android, Windows. Native apps later.
After the server is stable, investigate a shared client (React Native, Expo, Flutter, Tauri, …).
Do not choose the framework yet.

# Networking

LAN only during development (`http://192.168.x.x:<port>`). Never expose directly to the public Internet.
Later: secure remote access via WireGuard / Tailscale / another VPN.

# Windows Server Behavior

PC off → library unavailable. PC on → server starts → devices can access. Eventually auto-start on boot; maybe Wake-on-LAN.

# Licensing

Inspect licenses before copying code (Prismedia, Readest, BookOrbit, foliate-js, PDF.js).
Maintain `docs/THIRD_PARTY_LICENSES.md`.

# Development Philosophy

Do not rewrite Prismedia unnecessarily. Understand an existing feature, then extend it cleanly.
Preserve Prismedia as the media-server foundation while significantly upgrading the reading subsystem.

# End Goal

```text
Jellyfin + Readest + BookOrbit + Audiobookshelf
under one server, one account, one library, one consistent interface.
```

A **private, self-hosted personal media library with an unusually strong reading experience**.

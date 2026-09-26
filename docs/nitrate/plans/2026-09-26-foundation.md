# Nitrate Foundation (M0–M2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Tick boxes in this file and commit them as you go so a later session can resume.

**Goal:** Turn the empty `nitrate` repo into a working, documented snapshot fork of Prismedia, add an AMD AMF transcoder profile for the owner's Windows PC, and produce the architecture analysis plus the detailed plan for the first reader milestone (M3).

**Architecture:** Merge Prismedia's full git history at a pinned commit into `nitrate` (no upstream remote kept). Add Nitrate-specific agent instructions that override Prismedia's release/publishing rules. Analysis is evidence-based (every claim cites `path:line`). The only product code change in this plan is the AMF encoder profile; everything else is documentation and repo hygiene.

**Tech Stack (inherited from Prismedia):** .NET 10 (`net10.0`) API + Worker, EF Core + PostgreSQL 16, Svelte web app (`apps/web-svelte`) served as static assets by the API, pnpm 10.30.3 workspaces + Turbo, Vitest, xUnit, Playwright, FFmpeg for HLS, vendored foliate-js (`apps/web-svelte/src/lib/vendor/foliate-js`), `pdfjs-dist` ^6.

**Spec:** [`docs/nitrate/VISION.md`](../VISION.md) — read its **Decisions log** first. Roadmap: [`docs/nitrate/ROADMAP.md`](../ROADMAP.md).

## Global Constraints

- Prismedia snapshot commit: `627958a491f565babf1f8f693a5bb48ea834bc7e` (2026-09-18, v3.7.2). Do not fetch or merge anything newer.
- No `upstream`/`prismedia` git remote may remain after Task 1.
- Keep Prismedia's `LICENSE` file unchanged (CC BY-NC-SA 4.0). Do not copy code from Readest or BookOrbit.
- Existing media features and existing HTTP endpoints must keep working (closed-source Prismedia App Store apps depend on them).
- Target host is native Windows with AMD GPU; do not make Docker the recommended Windows path.
- Do not implement any reader feature (annotations, highlights, dictionary, …) in this plan. Task 8 only *writes* the M3 plan.
- `CLAUDE.md` is a symlink to `AGENTS.md` in Prismedia. Edit `AGENTS.md`; never replace the symlink with a file.
- Do not commit private IPs, hostnames, usernames, or home paths (Prismedia's Repository Privacy rule still applies). Placeholders like `192.168.x.x` and `D:\HomeServer` are fine.

## File Map

| Path | Change | Task |
|------|--------|------|
| whole Prismedia tree | imported via merge | 1 |
| `.github/workflows/publish-dev.yml`, `publish-channel.yml`, `documentation-site.yml` | delete | 2 |
| `.github/workflows/ci.yml` | change trigger to manual only | 2 |
| `AGENTS.md` (and `CLAUDE.md` via symlink) | prepend Nitrate overrides | 2 |
| `README.md` | prepend Nitrate header + credit | 2 |
| `docs/nitrate/BASELINE.md` | create: environment + baseline test results | 3 |
| `apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetServiceOptions.cs` | add `Amf` enum member | 4 |
| `apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.Encoding.cs` | map `Amf` → `h264_amf` | 4 |
| `apps/backend/src/Prismedia.Application/Settings/AppSettings.cs` | add `Amf` select option | 4 |
| `apps/web-svelte/src/lib/settings/library-settings.ts` | add `"Amf"` to `hlsTranscoderProfiles` | 4 |
| `apps/backend/tests/Prismedia.Infrastructure.Tests/HlsAssetServiceTests.cs` | AMF tests | 4 |
| `docs/nitrate/ARCHITECTURE_ANALYSIS.md` | create | 5 |
| `docs/THIRD_PARTY_LICENSES.md` | create | 6 |
| `docs/nitrate/WINDOWS_HOST.md` | create | 7 |
| `docs/nitrate/plans/<date>-m3-reader-slice-1.md` | create | 8 |
| `docs/nitrate/ROADMAP.md` | update status column | 2, 4, 7, 8 |

---

## M0 — Import & hygiene

### Task 1: Import the Prismedia snapshot with full history

**Files:** entire repository tree (merge). No manual edits.

**Interfaces:**
- Consumes: the current `nitrate` `main`, which contains only `docs/nitrate/**` (no `README.md`, no `.gitignore`), so the merge is conflict-free.
- Produces: a `main` whose history contains `627958a491f565babf1f8f693a5bb48ea834bc7e` and whose tree is Prismedia v3.7.2 plus `docs/nitrate/**`.

- [x] **Step 1: Confirm starting state**

```bash
git status --short          # expect: clean
git ls-files                # expect: only docs/nitrate/** files
```

- [x] **Step 2: Fetch Prismedia (full history, not shallow)**

```bash
git remote add prismedia https://github.com/pauljoda/Prismedia.git
git fetch prismedia main
git cat-file -t 627958a491f565babf1f8f693a5bb48ea834bc7e   # expect: commit
```

- [x] **Step 3: Merge the pinned commit**

```bash
git merge --allow-unrelated-histories --no-edit \
  -m "chore: import Prismedia v3.7.2 snapshot (627958a) as Nitrate base" \
  627958a491f565babf1f8f693a5bb48ea834bc7e
```

Expected: merge succeeds with no conflicts. If there is a conflict it can only be under `docs/nitrate/`; keep the Nitrate version (`git checkout --ours docs/nitrate && git add docs/nitrate`).

- [x] **Step 4: Remove the remote and verify**

```bash
git remote remove prismedia
git remote -v                                   # expect: only origin
git merge-base --is-ancestor 627958a491f565babf1f8f693a5bb48ea834bc7e HEAD && echo OK   # expect: OK
test -d apps/backend && test -d apps/web-svelte && test -f LICENSE && echo OK           # expect: OK
test -L CLAUDE.md && echo symlink                                                        # expect: symlink
```

- [x] **Step 5: Push**

```bash
git push origin main
```

---

### Task 2: Repo hygiene + Nitrate agent instructions

**Files:**
- Delete: `.github/workflows/publish-dev.yml`, `.github/workflows/publish-channel.yml`, `.github/workflows/documentation-site.yml`
- Modify: `.github/workflows/ci.yml` (trigger block only)
- Modify: `AGENTS.md` (prepend), `README.md` (prepend), `docs/nitrate/ROADMAP.md`

**Why:** `publish-dev.yml` runs on every push to `main` and tries to publish to Prismedia's GHCR; `ci.yml` runs on every push and would burn the private repo's Actions minutes. Prismedia's `AGENTS.md` also contains release/versioning/publishing rules that do not apply to Nitrate and would mislead agents.

- [x] **Step 1: Remove publishing workflows**

```bash
git rm .github/workflows/publish-dev.yml .github/workflows/publish-channel.yml .github/workflows/documentation-site.yml
```

- [x] **Step 2: Make CI manual-only.** In `.github/workflows/ci.yml` replace exactly this block:

```yaml
on:
  push:
    branches: [main]
  pull_request:
```

with:

```yaml
on:
  workflow_dispatch:
```

Verify: `ls .github/workflows` → `ci.yml  validate.yml`; `grep -n "^on:" -A 2 .github/workflows/*.yml` shows only `workflow_dispatch`.

- [x] **Step 3: Prepend Nitrate overrides to `AGENTS.md`.** Insert this block at the very top of `AGENTS.md`, above `# Prismedia Repository Contract`:

```markdown
# Nitrate Overrides (read first)

This repository is **Nitrate**, a private, personal, non-commercial snapshot fork of
[Prismedia](https://github.com/pauljoda/Prismedia) taken at `627958a` (v3.7.2). It is never
synced with upstream. The Prismedia contract below still applies **except** where this section
overrides it.

- **Spec and plans:** `docs/nitrate/VISION.md` (spec + decisions log), `docs/nitrate/ROADMAP.md`,
  `docs/nitrate/plans/`. Work from the active plan; tick its checkboxes as you go.
- **Overridden — do not follow:** "Release Channels", "Docker" publishing, GHCR images, and the
  SemVer/`CHANGELOG.md` bump rules in "Commit & Changelog Policy". Nitrate has no releases. Do not
  bump package versions. Do not edit `CHANGELOG.md`.
- **Still follow:** small conventional commits (`feat(api): …`, `fix(web): …`, `docs: …`),
  backend architecture contract, identifier discipline, UI composition rules, quality bar,
  repository privacy.
- **Tracked planning docs are allowed** under `docs/nitrate/` (this overrides the "keep roadmaps
  outside tracked documentation" rule for that folder only).
- **Target host:** native Windows on an AMD CPU/GPU. Keep code portable (no Linux-only paths or
  shell assumptions in runtime code). Docker remains supported but is not the Windows path.
- **Compatibility:** existing HTTP endpoints are used by the closed-source Prismedia iPhone/iPad/
  Apple TV apps. Do not change or remove them; add new endpoints instead.
- **Licensing:** never copy code from Readest (AGPL) or BookOrbit. Record every new third-party
  dependency in `docs/THIRD_PARTY_LICENSES.md`.
- **Verification:** before claiming a task done, run the relevant test commands and report their
  real output. Anything needing the Windows PC or a physical device is marked **Owner-verified**
  and must be listed for the owner, not claimed.

---

```

Verify: `head -3 CLAUDE.md` prints `# Nitrate Overrides (read first)` (proves the symlink still works).

- [x] **Step 4: Prepend Nitrate header to `README.md`.** Insert at the very top:

```markdown
# Nitrate

Private, self-hosted, all-in-one media library with a serious reading experience.
Nitrate is a personal fork of **[Prismedia](https://github.com/pauljoda/Prismedia)** by Paul Davis,
based on commit `627958a` (v3.7.2), used under
[CC BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/). Changes from Prismedia are
tracked in this repository's git history. Nitrate is not affiliated with or endorsed by Prismedia.

Project docs: [`docs/nitrate/`](docs/nitrate/).

---

```

- [x] **Step 5: Update roadmap** — set M0 Status to `Done` in `docs/nitrate/ROADMAP.md`.

- [x] **Step 6: Commit and push**

```bash
git add -A
git commit -m "chore: disable upstream publishing, add Nitrate agent overrides and credit"
git push origin main
```

---

### Task 3: Bring up the dev environment and record a baseline

**Files:** Create `docs/nitrate/BASELINE.md`.

**Interfaces:**
- Produces: `BASELINE.md` with sections `## Toolchain`, `## Test baseline`, `## Running locally`. Tasks 4–7 compare against the test baseline; a test that already failed here is not a regression.

- [x] **Step 1: Toolchain.** Install whatever is missing and record versions:

```bash
dotnet --version || (curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 && export PATH="$HOME/.dotnet:$PATH")
corepack enable && corepack prepare pnpm@10.30.3 --activate
node --version; pnpm --version; dotnet --version
ffmpeg -version | head -1    # install via apt if missing: sudo apt-get install -y ffmpeg
psql --version               # PostgreSQL 16 if integration tests or a local run need it
```

- [x] **Step 2: Install and run the suites**

```bash
pnpm install --frozen-lockfile
pnpm test:unit
pnpm test:web-svelte
pnpm test:backend
```

Record pass/fail counts for each in `BASELINE.md`. If a suite fails before any Nitrate code change, record the failing test names and the error — **do not fix them** in this task.

- [x] **Step 3: Try a local run** (API + worker + web). Read `docs/nix-development.md`, `infra/docker/docker-compose.yml`, and `apps/backend/src/Prismedia.Api/appsettings*.json` to find the required config (database connection string, `PRISMEDIA_DATA_DIR`, `PRISMEDIA_CACHE_DIR`, ffmpeg paths, port — README says `8008`). Start Postgres, then `pnpm dev:api`, `pnpm dev:worker`, `pnpm dev`. Record in `## Running locally` the exact env vars and commands that worked, or exactly where it failed. A failed local run in the cloud sandbox is acceptable; record it and continue.

- [x] **Step 4: Commit**

```bash
git add docs/nitrate/BASELINE.md
git commit -m "docs(nitrate): record toolchain and test baseline"
```

---

## M2 (part 1) — AMD AMF transcoder profile

### Task 4: Add the `Amf` HLS transcoder profile

Prismedia supports `Software`, `VideoToolbox`, `Vaapi`, `Nvenc`, `Qsv` but not AMD's Windows encoder. `Auto` on Windows resolves to `Software` (see `ResolveTranscoderProfile` in `HlsAssetService.Encoding.cs`); leave `Auto` unchanged — the owner will pick `Amf` explicitly in settings.

**Files:**
- Modify: `apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetServiceOptions.cs` (enum `HlsTranscoderProfile`, after `Qsv`)
- Modify: `apps/backend/src/Prismedia.Infrastructure/Videos/HlsAssetService.Encoding.cs` (`VideoEncoderArguments` switch)
- Modify: `apps/backend/src/Prismedia.Application/Settings/AppSettings.cs` (`TranscoderProfile` select options)
- Modify: `apps/web-svelte/src/lib/settings/library-settings.ts` (`hlsTranscoderProfiles`)
- Test: `apps/backend/tests/Prismedia.Infrastructure.Tests/HlsAssetServiceTests.cs`

**Interfaces:**
- Produces: `HlsTranscoderProfile.Amf`; persisted setting value `"Amf"` for key `hls.transcoderProfile`; ffmpeg `-c:v h264_amf`.

- [x] **Step 1: Write the failing tests.** Add next to `VirtualSegmentsUseVideoToolboxEncoderWhenConfigured` in `HlsAssetServiceTests.cs`:

```csharp
    [Fact]
    public async Task VirtualSegmentsUseAmfEncoderWhenConfigured() {
        var videoId = Guid.Parse("43434343-4343-4343-4343-434343434343");
        var sourcePath = Path.Combine(_cacheRoot, "source.mkv");
        await File.WriteAllTextAsync(sourcePath, "source");
        var process = new ManifestWritingProcessExecutor();
        var service = new HlsAssetService(
            new HlsAssetServiceOptions(_cacheRoot, HlsTranscoderProfile.Amf),
            new FakeVideoSourceService(new VideoSourceFile(
                videoId,
                sourcePath,
                "video/x-matroska",
                false,
                DurationSeconds: 13,
                Width: 1920,
                Height: 960)),
            process,
            NullLogger<HlsAssetService>.Instance);

        var segment = await service.GetAssetAsync(videoId, "v/720p/seg_00000.ts", null, CancellationToken.None);

        Assert.NotNull(segment);
        var arguments = Assert.Single(process.ArgumentHistory);
        Assert.Contains("h264_amf", arguments);
        Assert.Contains("yuv420p", string.Join(' ', arguments));
        Assert.DoesNotContain("libx264", arguments);
        Assert.DoesNotContain("-vaapi_device", arguments);
    }

    [Theory]
    [InlineData("Amf")]
    [InlineData("amf")]
    public void AmfProfileParsesFromSettingValue(string value) {
        Assert.Equal(HlsTranscoderProfile.Amf, HlsTranscoderProfiles.ParseOrDefault(value));
    }
```

- [x] **Step 2: Run to verify they fail**

```bash
dotnet test apps/backend/tests/Prismedia.Infrastructure.Tests --filter "FullyQualifiedName~Amf"
```

Expected: build error `'HlsTranscoderProfile' does not contain a definition for 'Amf'`.

- [x] **Step 3: Add the enum member.** In `HlsAssetServiceOptions.cs`, change the tail of the enum from:

```csharp
    /// <summary>Use Intel Quick Sync via <c>h264_qsv</c> when the host and ffmpeg build expose it.</summary>
    Qsv
}
```

to:

```csharp
    /// <summary>Use Intel Quick Sync via <c>h264_qsv</c> when the host and ffmpeg build expose it.</summary>
    Qsv,

    /// <summary>Use AMD's <c>h264_amf</c> encoder on Windows when the host and ffmpeg build expose it.</summary>
    Amf
}
```

- [x] **Step 4: Map the encoder.** In `HlsAssetService.Encoding.cs` `VideoEncoderArguments`, add the `Amf` arm:

```csharp
        var encoder = transcoderProfile switch {
            HlsTranscoderProfile.VideoToolbox => "h264_videotoolbox",
            HlsTranscoderProfile.Vaapi => "h264_vaapi",
            HlsTranscoderProfile.Nvenc => "h264_nvenc",
            HlsTranscoderProfile.Qsv => "h264_qsv",
            HlsTranscoderProfile.Amf => "h264_amf",
            _ => "libx264"
        };
```

No other change is needed in that method: the existing hardware branch already emits `-profile:v main` and `-pix_fmt yuv420p` for non-QSV, non-VAAPI profiles, both valid for `h264_amf`. The software-scale filter path (`format=yuv420p`) is also correct for AMF.

- [x] **Step 5: Run the tests to verify they pass**

```bash
dotnet test apps/backend/tests/Prismedia.Infrastructure.Tests --filter "FullyQualifiedName~HlsAssetServiceTests"
```

Expected: all `HlsAssetServiceTests` pass (new AMF tests included).

- [x] **Step 6: Expose it in settings (backend).** In `AppSettings.cs` `TranscoderProfile` options, add after the `Qsv` line (note the comma on the `Qsv` line):

```csharp
                new("Qsv", "QSV", "Intel Quick Sync."),
                new("Amf", "AMD AMF", "AMD on Windows.")
```

- [x] **Step 7: Expose it in settings (web).** In `apps/web-svelte/src/lib/settings/library-settings.ts`:

```ts
export const hlsTranscoderProfiles = [
  "Auto",
  "Software",
  "VideoToolbox",
  "Vaapi",
  "Nvenc",
  "Qsv",
  "Amf",
] as const;
```

Then search for tests or snapshots that enumerate profiles and update them if present:

```bash
grep -rn "\"Qsv\"\|'Qsv'\|Quick Sync" apps/web-svelte/src apps/backend/tests
```

If the settings UI has a label/description map keyed by profile (the grep will show it), add `Amf` there with label `AMD AMF`.

- [x] **Step 8: Run affected suites**

```bash
pnpm test:backend
pnpm test:web-svelte
pnpm check
pnpm api:check
```

If `pnpm api:check` reports drift in the generated client, run `pnpm api:generate` (it needs the dev API running — see BASELINE `## Running locally`) and include the regenerated files in the commit.

Expected: same results as `BASELINE.md` plus the new passing AMF tests. Any new failure must be fixed before committing.

- [x] **Step 9: Commit**

```bash
git add -A
git commit -m "feat(api): add AMD AMF (h264_amf) HLS transcoder profile for Windows hosts"
```

Real-hardware verification of AMF is **Owner-verified** in Task 7's checklist — do not claim it works on AMD hardware.

---

## M1 — Architecture analysis

### Task 5: Write `docs/nitrate/ARCHITECTURE_ANALYSIS.md`

**Files:** Create `docs/nitrate/ARCHITECTURE_ANALYSIS.md`.

**Rules:**
- Every factual claim cites evidence as a repo path with line, e.g. `apps/web-svelte/src/lib/components/BookFileReader.svelte:120`.
- For anything asked about that does not exist, write **"Not found"** and list what you searched (grep patterns, directories). Never infer that a feature exists from naming alone.
- Read Prismedia's own docs first — they are a map, not proof: `docs/architecture.md`, `docs/app-codebase-flow.md`, `docs/backend-architecture-contract.md`, `docs/consumption-tracking.md`, `docs/library-organization.md`, `docs/jobs.md`, `docs/database-migrations.md`, `documentation-site/docs/library/books.md`.

**Interfaces:**
- Produces: the section headings below, in this order. Task 7 depends on §19; Task 8 depends on §10–§14 and §16.

- [x] **Step 1: Write sections 1–18** (the owner's list), with these starting points:

| § | Topic | Start here |
|---|-------|-----------|
| 1 | Directory structure | `AGENTS.md` "Project Structure", `apps/`, `packages/`, `infra/`, `scripts/` |
| 2 | Frontend technology | `apps/web-svelte/package.json`, `packages/ui-svelte` |
| 3 | Backend technology | `apps/backend/Prismedia.slnx`, `apps/backend/src/*` projects and their layering |
| 4 | Database | `apps/backend/src/Prismedia.Infrastructure/Persistence`, migrations, entity list |
| 5 | Authentication | grep `AddAuthentication`, `Authorize`, `JwtBearer`, `Cookie`, user entity; single vs multi-user |
| 6 | API structure | `Prismedia.Api` endpoint registration, OpenAPI generation, `apps/web-svelte/src/lib/api/generated` (orval) |
| 7 | Media-library model | entity types, how books/comics/audio/video share identity |
| 8 | Scanning | `Prismedia.Worker`, jobs, file watching (note Windows-relevant behaviour) |
| 9 | FFmpeg/transcoding | `apps/backend/src/Prismedia.Infrastructure/Videos/`, direct play vs HLS decision |
| 10 | How books are represented | book entities, files, formats (EPUB/PDF/CBZ/…), `apps/web-svelte/src/lib/api/books.ts` |
| 11 | EPUB rendering | `apps/web-svelte/src/lib/components/BookFileReader.svelte`, `apps/web-svelte/src/lib/vendor/foliate-js/` (which foliate-js version/commit? which modules: `view.js`, `epubcfi.js`, overlayer?), `foliate-vendor.d.ts` |
| 12 | PDF rendering | `pdfjs-dist` usage, and whether PDFs go through foliate-js's `pdf.js` adapter |
| 13 | Reading progress storage | `docs/consumption-tracking.md`, progress entities/endpoints; what locator is stored (CFI? percentage? page?) |
| 14 | Existing annotations/bookmarks | grep `annotation`, `bookmark`, `highlight`, `note` in `apps/backend/src` (excluding `Migrations/`) and `apps/web-svelte/src` |
| 15 | Apple-client/API architecture | apps are closed-source; document which API surface is public/versioned, auth flow for native clients, any client-capability negotiation (`apps/web-svelte/src/lib/api/capabilities.ts`) |
| 16 | Best extension points for our reader | concrete files/classes where annotations, selection menu, and providers plug in |
| 17 | Relevant licenses | Prismedia, vendored foliate-js, pdfjs-dist; Readest/BookOrbit (reference only) — link to `docs/THIRD_PARTY_LICENSES.md` |
| 18 | Risks / limitations | include: foliate-js being vendored (upgrade path), CFI stability across re-scans/file changes, multi-user assumptions, anything that blocks native Windows |

- [x] **Step 2: Add §19 "Native Windows readiness".** Evidence-based checklist of anything that assumes Linux/Docker in runtime code or startup:

```bash
grep -rnE "OperatingSystem\.IsLinux|/dev/|/tmp|/data\b|chmod|\"bash\"|\"sh\"|Path\.Combine\(\"/|inotify|Mono\.Unix" apps/backend/src --include='*.cs' | grep -v /Migrations/
grep -rnE "PRISMEDIA_[A-Z_]+" apps/backend/src --include='*.cs' -o | sort -u
```

For each hit, say whether it breaks on Windows and the minimal fix. Also list every `PRISMEDIA_*` env var with its default and meaning (Task 7 needs this), and how the API finds the built web assets.

- [x] **Step 3: Add §20 "Proposed annotation system (summary)"** — one page: data model, EPUB locator choice, PDF locator choice, sync semantics, endpoints, frontend integration points. This is the summary Task 8 expands into a full plan.

- [x] **Step 4: Commit**

```bash
git add docs/nitrate/ARCHITECTURE_ANALYSIS.md
git commit -m "docs(nitrate): architecture analysis of Prismedia base"
```

### Task 6: Write `docs/THIRD_PARTY_LICENSES.md`

**Files:** Create `docs/THIRD_PARTY_LICENSES.md`.

- [x] **Step 1: Gather**

```bash
pnpm licenses list --prod --json > /tmp/js-licenses.json
dotnet list apps/backend/Prismedia.slnx package --include-transitive > /tmp/dotnet-packages.txt
head -40 apps/web-svelte/src/lib/vendor/foliate-js/*.js | grep -iE "license|copyright" | sort -u
```

- [x] **Step 2: Write the file** with sections: `## Base project` (Prismedia, CC BY-NC-SA 4.0, commit `627958a`), `## Vendored code` (foliate-js: license + upstream URL + vendored version if determinable), `## JavaScript dependencies (production)` (table: package, version, license — group by license), `## .NET dependencies` (table: package, version, license — look up license from NuGet metadata where the tool does not report it), `## Reference-only projects (no code used)` (Readest, BookOrbit, KOReader — with their licenses). Flag any GPL/AGPL/SSPL/non-commercial license in production dependencies at the top under `## Attention`.

- [x] **Step 3: Commit**

```bash
git add docs/THIRD_PARTY_LICENSES.md
git commit -m "docs: third-party license inventory"
```

---

## M2 (part 2) — Windows host

### Task 7: Write `docs/nitrate/WINDOWS_HOST.md` and fix small Windows blockers

**Files:** Create `docs/nitrate/WINDOWS_HOST.md`. Modify runtime code only for Windows blockers identified in ARCHITECTURE_ANALYSIS §19 that are small and local (e.g. a hard-coded `/` path); larger blockers get listed, not fixed.

**Interfaces:**
- Consumes: ARCHITECTURE_ANALYSIS §19 (env vars, blockers, web-asset path), BASELINE `## Running locally`.

- [ ] **Step 1: Fix small blockers** from §19 one at a time, each with a unit test where the code is testable, each as its own commit (`fix(api): …`). Run `pnpm test:backend` after each.

- [ ] **Step 2: Write the guide** with these sections, using real commands and the env vars found in §19:
  1. **Prerequisites (winget):** .NET 10 SDK, Node LTS + pnpm (via corepack), PostgreSQL 16, FFmpeg full build (the gyan.dev build includes `h264_amf`), Git. Give the `winget install` commands and tell the owner to confirm IDs with `winget search <name>` if one fails.
  2. **Library folders:** `D:\HomeServer\{Movies,TV,Music,Audiobooks,Books,Comics,Photos}` and how to add each as a library in the web UI.
  3. **Database:** create the Postgres user/database; the connection string format.
  4. **Build:** clone, `pnpm install`, build the web app, `dotnet publish` the API and Worker (Release) to a folder such as `C:\Nitrate\`.
  5. **Configure:** every required `PRISMEDIA_*` env var with a Windows value (data/cache on a fast drive, ffmpeg/ffprobe paths).
  6. **Run:** start API and Worker; open `http://localhost:8008`; complete the first-run wizard.
  7. **Enable AMD encoding:** Settings → HLS transcoder → `AMD AMF`.
  8. **LAN access:** firewall rule scoped to Private networks only:
     ```powershell
     New-NetFirewallRule -DisplayName "Nitrate" -Direction Inbound -Protocol TCP -LocalPort 8008 -Profile Private -Action Allow
     ```
     Then open `http://<PC LAN IP>:8008` from other devices. Do **not** port-forward on the router.
  9. **Known blockers** (from §19 that were not fixed), each with a workaround if one exists.
     Include the note: on Windows, git checks out the `CLAUDE.md → AGENTS.md` symlink as a small
     text file unless Developer Mode + `core.symlinks=true` are on. This is harmless for running
     the server.

- [ ] **Step 3: Owner verification checklist** — end the guide with this checklist (unticked; the owner fills it in):

```markdown
## Owner verification

- [ ] `ffmpeg -hide_banner -encoders | findstr amf` lists `h264_amf`
- [ ] Server starts natively (no Docker) and the web UI loads at http://localhost:8008
- [ ] Each test library scans and shows items (Movies, TV, Music, Audiobooks, Books, Comics, Photos)
- [ ] A video that needs transcoding plays; Task Manager → GPU shows "Video Encode" activity; CPU stays moderate
- [ ] A direct-play video plays with near-zero CPU
- [ ] EPUB and PDF open in the reader
- [ ] Reachable from: Mac browser / iPad / Android phone over LAN
- [ ] Prismedia iPhone/iPad App Store app can connect to the server
- [ ] PC stays usable (normal apps, light gaming) while one stream transcodes
```

- [ ] **Step 4: Update roadmap** — M2 Status: `Built; awaiting owner verification`.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "docs(nitrate): native Windows host guide with AMD AMF setup"
git push origin main
```

---

## Hand-off to M3

### Task 8: Write the M3 reader-slice-1 plan (do not execute it)

**Files:** Create `docs/nitrate/plans/<YYYY-MM-DD>-m3-reader-slice-1.md`.

**Interfaces:**
- Consumes: ARCHITECTURE_ANALYSIS §10–§14, §16, §20; VISION "Highlights", "Notes", "Progress Synchronization".

- [ ] **Step 1: Write the plan** using the superpowers:writing-plans skill format (header, Global Constraints, file map, TDD tasks with real code). Scope is **exactly**:
  - Server: `Annotation` entity + EF migration (fields from VISION's `Annotation` shape: id, userId, bookId, locator, selectedText, surroundingText, type, color, note, createdAt, updatedAt; plus soft-delete `deletedAt` for sync), CRUD endpoints under a new route group, per-user authorization, OpenAPI + regenerated web client (`pnpm api:generate`).
  - Sync semantics: server is the source of truth; client fetches all annotations for a book on open and after focus/visibility change; last-write-wins on `updatedAt`; deletes are tombstones.
  - EPUB locator: CFI via the vendored foliate-js `epubcfi.js` unless §11/§16 found a better foliate-js mechanism; store a text-quote fallback (`selectedText` + `surroundingText`) to re-anchor if the CFI fails.
  - Reader: selection menu with **Highlight** (5 colours) and **Add Note** only (Define/Translate/Wikipedia/Search arrive in M5; the menu should be built so those are added as entries, not a rewrite); render highlights with foliate-js's overlay mechanism; tap a highlight to change colour, edit/delete note.
  - Out of scope: bookmarks, `/annotations` page, PDF, dictionary, translation, underline/strikethrough styles.
  - A Playwright e2e test: open an EPUB fixture, highlight, reload, highlight still there; second browser context (same user) sees it.
  - Owner-verified acceptance: highlight on Mac browser, see it on iPad.

- [ ] **Step 2: Update roadmap** — M1 Status: `Done`; M3 Plan column: link to the new file; M3 Status: `Plan ready — awaiting owner review`.

- [ ] **Step 3: Commit, push, and stop**

```bash
git add docs/nitrate
git commit -m "docs(nitrate): M3 reader slice 1 plan"
git push origin main
```

**Stop here.** Report to the owner: baseline results, AMF status, Windows blockers found, and the M3 plan for review. Do not begin M3 in this session.

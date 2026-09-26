# Third-Party Licenses

Inventory for Nitrate (a private, personal, non-commercial fork of Prismedia). Generated 2026-09-26 from
`pnpm licenses list --prod --json` (per workspace) and `dotnet list apps/backend/Prismedia.slnx package --include-transitive`,
with .NET licenses read from each package's `.nuspec` in the local NuGet cache. Regenerate with the same
commands when dependencies change; record every **new** dependency here (see `AGENTS.md` Nitrate overrides).

## Attention

Nitrate is private and not distributed, so copyleft obligations (which trigger on distribution/conveyance) are not
currently engaged. These are flagged so that any future sharing of builds is a conscious decision.

| Package | Version | License | Where | Note |
|---|---|---|---|---|
| Prismedia (base project) | 627958a (v3.7.2) | **CC BY-NC-SA 4.0** | whole repo | Non-commercial + share-alike. Nitrate must stay non-commercial; any shared derivative must use the same license and credit Prismedia. |
| `rvfc-polyfill` | 1.0.8 | **GPL-3.0** | web bundle, via `jassub` | Copyleft. Pulled in transitively by the subtitle renderer `jassub`. |
| `jassub` | 2.4.2 | **LGPL-2.1-or-later AND (FTL OR GPL-2.0-or-later) AND MIT AND …** | web bundle (direct dependency) | libass/FreeType WASM build for ASS/SSA subtitles. LGPL/GPL components. |
| `@pierre/trees` | 1.0.0-beta.4 | apache-2.0 (lower-case in metadata) | web bundle | Pre-release package; license string non-canonical but Apache-2.0. |
| `dompurify` | 3.4.10 | MPL-2.0 OR Apache-2.0 | ui-svelte | Dual-licensed; Apache-2.0 option applies. |
| `lightningcss` (+ native binary) | 1.32.0 | MPL-2.0 | ui-svelte build tooling (Tailwind) | File-level copyleft; build-time only, not in the served bundle. |
| `VersOne.Epub` | 3.3.6 | Unlicense | .NET runtime (EPUB parsing) | Public-domain dedication; no obligations. |
| `Npgsql`, `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.2 / 10.0.1 | PostgreSQL License | .NET runtime | Permissive (BSD-like). |

No AGPL or SSPL licenses and no other non-commercial licenses were found in production dependencies. The
documentation site (`documentation-site/`, Docusaurus) is not part of the running server; its extra licenses
(e.g. `lunr-languages` MPL-1.1, `caniuse-lite` CC-BY-4.0) appear only in the root-level `--prod` listing.

## Base project

- **Prismedia** — https://github.com/pauljoda/Prismedia — snapshot commit `627958a491f565babf1f8f693a5bb48ea834bc7e`
  (2026-09-18, v3.7.2). License: **CC BY-NC-SA 4.0**, `LICENSE` (Copyright (c) 2025 Paul Davis). Kept unchanged.
  Credited in `README.md`.

## Vendored code

| Component | Location | Upstream | Version | License |
|---|---|---|---|---|
| foliate-js | `apps/web-svelte/src/lib/vendor/foliate-js/` | https://github.com/johnfactotum/foliate-js | commit `78914aef4466eb960965702401634c2cb348e9b1` (per `PROVENANCE.md`) | MIT, Copyright (c) 2022 John Factotum (`LICENSE`) |
| zip.js (bundled by foliate-js) | `…/foliate-js/vendor/zip.js` | https://github.com/gildas-lormeau/zip.js | not stated | No license header in the vendored minified file; upstream zip.js is BSD-3-Clause (not verifiable in-repo) |
| fflate (bundled by foliate-js) | `…/foliate-js/vendor/fflate.js` | https://github.com/101arrowz/fflate | not stated | No license header in the vendored file; upstream fflate is MIT (not verifiable in-repo) |

Local modification: foliate-js `pdf.js` is stubbed and `vendor/pdfjs/` was removed (see `PROVENANCE.md`).

## JavaScript dependencies (production)

Production dependency trees of `@prismedia/web-svelte` (101 packages) and `@prismedia/ui-svelte` (84 packages).
The web app is compiled into static assets, so some packages declared as devDependencies are also compiled into
the shipped bundle; those are listed separately below.

### Directly declared runtime dependencies (`apps/web-svelte/package.json`)

| Package | Version | License |
|---|---|---|
| `@fontsource/cinzel` | 5.2.8 | OFL-1.1 |
| `@pierre/trees` | 1.0.0-beta.4 | Apache-2.0 |
| `@tiptap/core` | 3.23.6 | MIT |
| `@tiptap/extension-placeholder` | 3.23.6 | MIT |
| `@tiptap/pm` | 3.23.6 | MIT |
| `@tiptap/starter-kit` | 3.23.6 | MIT |
| `hls.js` | 1.6.16 | Apache-2.0 |
| `jassub` | 2.4.2 | LGPL-2.1-or-later AND (FTL OR GPL-2.0-or-later) AND MIT AND MIT-Modern-Variant AND ISC AND NTP AND Zlib AND BSL-1.0 |
| `marked` | 18.0.4 | MIT |
| `pdfjs-dist` | 6.0.227 | Apache-2.0 |
| `svelte-dnd-action` | 0.9.69 | MIT |
| `tiptap-markdown` | 0.9.0 | MIT |
| `vidstack` | 1.12.13 | MIT |

### devDependencies compiled into the shipped bundle

| Package | Version | License |
|---|---|---|
| `svelte` | 5.55.9 | MIT |
| `@sveltejs/kit` | 2.61.0 | MIT |
| `@lucide/svelte` | 1.16.0 | ISC |
| `@fontsource-variable/inter` | 5.2.8 | OFL-1.1 |
| `@fontsource-variable/jetbrains-mono` | 5.2.8 | OFL-1.1 |
| `@fontsource/geist-sans` | 5.2.5 | OFL-1.1 |

### Full production tree, grouped by license

<details><summary><b>MIT</b> — 140 package(s)</summary>

| Package | Version | Used by |
|---|---|---|
| `@esbuild/linux-x64` | 0.27.7 | ui-svelte |
| `@esbuild/linux-x64` | 0.28.0 | ui-svelte |
| `@floating-ui/core` | 1.7.5 | ui-svelte, web-svelte |
| `@floating-ui/dom` | 1.7.6 | ui-svelte, web-svelte |
| `@floating-ui/utils` | 0.2.11 | ui-svelte, web-svelte |
| `@jridgewell/gen-mapping` | 0.3.13 | ui-svelte, web-svelte |
| `@jridgewell/remapping` | 2.3.5 | ui-svelte, web-svelte |
| `@jridgewell/resolve-uri` | 3.1.2 | ui-svelte, web-svelte |
| `@jridgewell/source-map` | 0.3.11 | ui-svelte |
| `@jridgewell/sourcemap-codec` | 1.5.5 | ui-svelte, web-svelte |
| `@jridgewell/trace-mapping` | 0.3.31 | ui-svelte, web-svelte |
| `@napi-rs/canvas` | 1.0.0 | web-svelte |
| `@napi-rs/canvas-linux-x64-gnu` | 1.0.0 | web-svelte |
| `@oxc-project/types` | 0.124.0 | ui-svelte |
| `@polka/url` | 1.0.0-next.29 | ui-svelte |
| `@rolldown/binding-linux-x64-gnu` | 1.0.0-rc.15 | ui-svelte |
| `@rolldown/pluginutils` | 1.0.0-rc.15 | ui-svelte |
| `@standard-schema/spec` | 1.1.0 | ui-svelte |
| `@sveltejs/acorn-typescript` | 1.0.10 | ui-svelte, web-svelte |
| `@sveltejs/kit` | 2.61.0 | ui-svelte |
| `@sveltejs/vite-plugin-svelte` | 5.1.1 | ui-svelte |
| `@sveltejs/vite-plugin-svelte-inspector` | 4.0.1 | ui-svelte |
| `@tiptap/core` | 3.23.6 | web-svelte |
| `@tiptap/extension-blockquote` | 3.23.6 | web-svelte |
| `@tiptap/extension-bold` | 3.23.6 | web-svelte |
| `@tiptap/extension-bullet-list` | 3.23.6 | web-svelte |
| `@tiptap/extension-code` | 3.23.6 | web-svelte |
| `@tiptap/extension-code-block` | 3.23.6 | web-svelte |
| `@tiptap/extension-document` | 3.23.6 | web-svelte |
| `@tiptap/extension-dropcursor` | 3.23.6 | web-svelte |
| `@tiptap/extension-gapcursor` | 3.23.6 | web-svelte |
| `@tiptap/extension-hard-break` | 3.23.6 | web-svelte |
| `@tiptap/extension-heading` | 3.23.6 | web-svelte |
| `@tiptap/extension-horizontal-rule` | 3.23.6 | web-svelte |
| `@tiptap/extension-italic` | 3.23.6 | web-svelte |
| `@tiptap/extension-link` | 3.23.6 | web-svelte |
| `@tiptap/extension-list` | 3.23.6 | web-svelte |
| `@tiptap/extension-list-item` | 3.23.6 | web-svelte |
| `@tiptap/extension-list-keymap` | 3.23.6 | web-svelte |
| `@tiptap/extension-ordered-list` | 3.23.6 | web-svelte |
| `@tiptap/extension-paragraph` | 3.23.6 | web-svelte |
| `@tiptap/extension-placeholder` | 3.23.6 | web-svelte |
| `@tiptap/extension-strike` | 3.23.6 | web-svelte |
| `@tiptap/extension-text` | 3.23.6 | web-svelte |
| `@tiptap/extension-underline` | 3.23.6 | web-svelte |
| `@tiptap/extensions` | 3.23.6 | web-svelte |
| `@tiptap/pm` | 3.23.6 | web-svelte |
| `@tiptap/starter-kit` | 3.23.6 | web-svelte |
| `@types/cookie` | 0.6.0 | ui-svelte |
| `@types/estree` | 1.0.9 | ui-svelte, web-svelte |
| `@types/linkify-it` | 3.0.5 | web-svelte |
| `@types/linkify-it` | 5.0.0 | web-svelte |
| `@types/markdown-it` | 13.0.9 | web-svelte |
| `@types/markdown-it` | 14.1.2 | web-svelte |
| `@types/mdurl` | 1.0.5 | web-svelte |
| `@types/mdurl` | 2.0.0 | web-svelte |
| `@types/node` | 25.9.1 | ui-svelte |
| `@types/trusted-types` | 2.0.7 | ui-svelte, web-svelte |
| `acorn` | 8.16.0 | ui-svelte, web-svelte |
| `bits-ui` | 2.19.0 | ui-svelte |
| `buffer-from` | 1.1.2 | ui-svelte |
| `clsx` | 2.1.1 | ui-svelte, web-svelte |
| `commander` | 2.20.3 | ui-svelte |
| `cookie` | 0.6.0 | ui-svelte |
| `debug` | 4.4.3 | ui-svelte |
| `deepmerge` | 4.3.1 | ui-svelte |
| `dequal` | 2.0.3 | ui-svelte |
| `devalue` | 5.8.1 | ui-svelte, web-svelte |
| `esbuild` | 0.27.7 | ui-svelte |
| `esbuild` | 0.28.0 | ui-svelte |
| `esm-env` | 1.2.2 | ui-svelte, web-svelte |
| `esrap` | 2.2.9 | ui-svelte, web-svelte |
| `fdir` | 6.5.0 | ui-svelte |
| `get-tsconfig` | 4.14.0 | ui-svelte |
| `inline-style-parser` | 0.2.7 | ui-svelte |
| `is-reference` | 3.0.3 | ui-svelte, web-svelte |
| `jiti` | 2.7.0 | ui-svelte |
| `kleur` | 4.1.5 | ui-svelte |
| `lfa-ponyfill` | 1.1.0 | web-svelte |
| `linkify-it` | 5.0.1 | web-svelte |
| `linkifyjs` | 4.3.3 | web-svelte |
| `locate-character` | 3.0.0 | ui-svelte, web-svelte |
| `lz-string` | 1.5.0 | ui-svelte |
| `magic-string` | 0.30.21 | ui-svelte, web-svelte |
| `markdown-it` | 14.1.1 | web-svelte |
| `marked` | 18.0.4 | web-svelte |
| `mdurl` | 2.0.0 | web-svelte |
| `media-captions` | 1.0.4 | web-svelte |
| `mrmime` | 2.0.1 | ui-svelte |
| `ms` | 2.1.3 | ui-svelte |
| `nanoid` | 3.3.12 | ui-svelte |
| `orderedmap` | 2.1.1 | web-svelte |
| `picomatch` | 4.0.4 | ui-svelte |
| `postcss` | 8.5.15 | ui-svelte |
| `preact` | 11.0.0-beta.0 | web-svelte |
| `preact-render-to-string` | 6.6.5 | web-svelte |
| `prosemirror-changeset` | 2.4.1 | web-svelte |
| `prosemirror-commands` | 1.7.1 | web-svelte |
| `prosemirror-dropcursor` | 1.8.2 | web-svelte |
| `prosemirror-gapcursor` | 1.4.1 | web-svelte |
| `prosemirror-history` | 1.5.0 | web-svelte |
| `prosemirror-keymap` | 1.2.3 | web-svelte |
| `prosemirror-markdown` | 1.13.4 | web-svelte |
| `prosemirror-model` | 1.25.7 | web-svelte |
| `prosemirror-schema-list` | 1.5.1 | web-svelte |
| `prosemirror-state` | 1.4.4 | web-svelte |
| `prosemirror-tables` | 1.8.5 | web-svelte |
| `prosemirror-transform` | 1.12.0 | web-svelte |
| `prosemirror-view` | 1.41.8 | web-svelte |
| `punycode.js` | 2.3.1 | web-svelte |
| `react` | 19.2.6 | web-svelte |
| `react-dom` | 19.2.6 | web-svelte |
| `resolve-pkg-maps` | 1.0.0 | ui-svelte |
| `rolldown` | 1.0.0-rc.15 | ui-svelte |
| `rope-sequence` | 1.3.4 | web-svelte |
| `runed` | 0.35.1 | ui-svelte |
| `scheduler` | 0.27.0 | web-svelte |
| `set-cookie-parser` | 3.1.0 | ui-svelte |
| `sirv` | 3.0.2 | ui-svelte |
| `source-map-support` | 0.5.21 | ui-svelte |
| `style-to-object` | 1.0.14 | ui-svelte |
| `svelte` | 5.55.9 | ui-svelte, web-svelte |
| `svelte-dnd-action` | 0.9.69 | web-svelte |
| `svelte-toolbelt` | 0.10.6 | ui-svelte |
| `tabbable` | 6.5.0 | ui-svelte |
| `tailwind-merge` | 3.6.0 | ui-svelte |
| `throughput` | 1.0.2 | web-svelte |
| `tinyglobby` | 0.2.16 | ui-svelte |
| `tiptap-markdown` | 0.9.0 | web-svelte |
| `totalist` | 3.0.1 | ui-svelte |
| `tsx` | 4.21.0 | ui-svelte |
| `uc.micro` | 2.1.0 | web-svelte |
| `undici-types` | 7.24.6 | ui-svelte |
| `unplugin` | 1.16.1 | web-svelte |
| `vidstack` | 1.12.13 | web-svelte |
| `vite` | 8.0.8 | ui-svelte |
| `vitefu` | 1.1.3 | ui-svelte |
| `w3c-keyname` | 2.2.8 | web-svelte |
| `webpack-virtual-modules` | 0.6.2 | web-svelte |
| `zimmerframe` | 1.1.4 | ui-svelte, web-svelte |

</details>

<details><summary><b>Apache-2.0</b> — 11 package(s)</summary>

| Package | Version | Used by |
|---|---|---|
| `@internationalized/date` | 3.12.4 | ui-svelte |
| `@pierre/trees` | 1.0.0-beta.4 | web-svelte |
| `@swc/helpers` | 0.5.23 | ui-svelte |
| `abslink` | 1.2.2 | web-svelte |
| `aria-query` | 5.3.1 | ui-svelte, web-svelte |
| `axobject-query` | 4.1.0 | ui-svelte, web-svelte |
| `class-variance-authority` | 0.7.1 | ui-svelte |
| `detect-libc` | 2.1.2 | ui-svelte |
| `hls.js` | 1.6.16 | web-svelte |
| `pdfjs-dist` | 6.0.227 | web-svelte |
| `typescript` | 5.9.3 | ui-svelte |

</details>

<details><summary><b>ISC</b> — 4 package(s)</summary>

| Package | Version | Used by |
|---|---|---|
| `@lucide/svelte` | 1.16.0 | ui-svelte |
| `markdown-it-task-lists` | 2.1.1 | web-svelte |
| `picocolors` | 1.1.1 | ui-svelte |
| `yaml` | 2.9.0 | ui-svelte |

</details>

<details><summary><b>BSD-3-Clause</b> — 3 package(s)</summary>

| Package | Version | Used by |
|---|---|---|
| `lit-html` | 2.8.0 | web-svelte |
| `source-map` | 0.6.1 | ui-svelte |
| `source-map-js` | 1.2.1 | ui-svelte |

</details>

<details><summary><b>BSD-2-Clause</b> — 2 package(s)</summary>

| Package | Version | Used by |
|---|---|---|
| `entities` | 4.5.0 | web-svelte |
| `terser` | 5.48.0 | ui-svelte |

</details>

<details><summary><b>MPL-2.0</b> — 2 package(s)</summary>

| Package | Version | Used by |
|---|---|---|
| `lightningcss` | 1.32.0 | ui-svelte |
| `lightningcss-linux-x64-gnu` | 1.32.0 | ui-svelte |

</details>

<details><summary><b>0BSD</b> — 1 package(s)</summary>

| Package | Version | Used by |
|---|---|---|
| `tslib` | 2.8.1 | ui-svelte |

</details>

<details><summary><b>GPL-3.0</b> — 1 package(s)</summary>

| Package | Version | Used by |
|---|---|---|
| `rvfc-polyfill` | 1.0.8 | web-svelte |

</details>

<details><summary><b>LGPL-2.1-or-later AND (FTL OR GPL-2.0-or-later) AND MIT AND MIT-Modern-Variant AND ISC AND NTP AND Zlib AND BSL-1.0</b> — 1 package(s)</summary>

| Package | Version | Used by |
|---|---|---|
| `jassub` | 2.4.2 | web-svelte |

</details>

<details><summary><b>OFL-1.1</b> — 1 package(s)</summary>

| Package | Version | Used by |
|---|---|---|
| `@fontsource/cinzel` | 5.2.8 | web-svelte |

</details>

<details><summary><b>Python-2.0</b> — 1 package(s)</summary>

| Package | Version | Used by |
|---|---|---|
| `argparse` | 2.0.1 | web-svelte |

</details>

## .NET dependencies

From `dotnet list apps/backend/Prismedia.slnx package --include-transitive` (100 packages). License = `.nuspec`
license expression; two packages only publish a license URL and are resolved by hand (marked †).

### Runtime (API, Worker, Application, Infrastructure) — 83 packages

| Package | Version | License | Top-level / transitive |
|---|---|---|---|
| `AngleSharp` | 1.5.2 | MIT | top |
| `AngleSharp.XPath` | 2.0.6 | MIT | top |
| `bblanchon.PDFium.Linux` | 130.0.6721 | Apache-2.0 | transitive |
| `bblanchon.PDFium.macOS` | 130.0.6721 | Apache-2.0 | transitive |
| `bblanchon.PDFium.Win32` | 130.0.6721 | Apache-2.0 | transitive |
| `Humanizer.Core` | 2.14.1 | MIT | transitive |
| `Microsoft.AspNetCore.Cryptography.Internal` | 10.0.8 | MIT | transitive |
| `Microsoft.AspNetCore.Cryptography.KeyDerivation` | 10.0.8 | MIT | transitive |
| `Microsoft.AspNetCore.OpenApi` | 10.0.9 | MIT | top |
| `Microsoft.Build.Framework` | 18.0.2 | MIT | transitive |
| `Microsoft.CodeAnalysis.Analyzers` | 3.11.0 | MIT | transitive |
| `Microsoft.CodeAnalysis.Common` | 5.0.0 | MIT | transitive |
| `Microsoft.CodeAnalysis.CSharp` | 5.0.0 | MIT | transitive |
| `Microsoft.CodeAnalysis.CSharp.Workspaces` | 5.0.0 | MIT | transitive |
| `Microsoft.CodeAnalysis.Workspaces.Common` | 5.0.0 | MIT | transitive |
| `Microsoft.CodeAnalysis.Workspaces.MSBuild` | 5.0.0 | MIT | transitive |
| `Microsoft.EntityFrameworkCore` | 10.0.8 | MIT | top |
| `Microsoft.EntityFrameworkCore.Abstractions` | 10.0.8 | MIT | transitive |
| `Microsoft.EntityFrameworkCore.Analyzers` | 10.0.8 | MIT | transitive |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.8 | MIT | top |
| `Microsoft.EntityFrameworkCore.Relational` | 10.0.8 | MIT | top |
| `Microsoft.Extensions.Caching.Abstractions` | 10.0.8 | MIT | transitive |
| `Microsoft.Extensions.Caching.Memory` | 10.0.8 | MIT | transitive |
| `Microsoft.Extensions.Configuration` | 10.0.8 | MIT | transitive |
| `Microsoft.Extensions.Configuration.Abstractions` | 10.0.5 | MIT | top |
| `Microsoft.Extensions.Configuration.Abstractions` | 10.0.8 | MIT | top |
| `Microsoft.Extensions.Configuration.Binder` | 10.0.8 | MIT | transitive |
| `Microsoft.Extensions.Configuration.CommandLine` | 10.0.5 | MIT | transitive |
| `Microsoft.Extensions.Configuration.EnvironmentVariables` | 10.0.5 | MIT | transitive |
| `Microsoft.Extensions.Configuration.FileExtensions` | 10.0.5 | MIT | transitive |
| `Microsoft.Extensions.Configuration.Json` | 10.0.5 | MIT | transitive |
| `Microsoft.Extensions.Configuration.UserSecrets` | 10.0.5 | MIT | transitive |
| `Microsoft.Extensions.DependencyInjection` | 10.0.8 | MIT | transitive |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | 10.0.8 | MIT | top |
| `Microsoft.Extensions.DependencyModel` | 10.0.5 | MIT | transitive |
| `Microsoft.Extensions.DependencyModel` | 10.0.8 | MIT | transitive |
| `Microsoft.Extensions.Diagnostics` | 10.0.8 | MIT | transitive |
| `Microsoft.Extensions.Diagnostics.Abstractions` | 10.0.5 | MIT | transitive |
| `Microsoft.Extensions.Diagnostics.Abstractions` | 10.0.8 | MIT | transitive |
| `Microsoft.Extensions.FileProviders.Abstractions` | 10.0.5 | MIT | transitive |
| `Microsoft.Extensions.FileProviders.Physical` | 10.0.5 | MIT | transitive |
| `Microsoft.Extensions.FileSystemGlobbing` | 10.0.5 | MIT | transitive |
| `Microsoft.Extensions.Hosting` | 10.0.5 | MIT | top |
| `Microsoft.Extensions.Hosting.Abstractions` | 10.0.5 | MIT | top |
| `Microsoft.Extensions.Http` | 10.0.8 | MIT | top |
| `Microsoft.Extensions.Identity.Core` | 10.0.8 | MIT | top |
| `Microsoft.Extensions.Logging` | 10.0.8 | MIT | transitive |
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.5 | MIT | transitive |
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.8 | MIT | transitive |
| `Microsoft.Extensions.Logging.Configuration` | 10.0.5 | MIT | transitive |
| `Microsoft.Extensions.Logging.Console` | 10.0.5 | MIT | transitive |
| `Microsoft.Extensions.Logging.Debug` | 10.0.5 | MIT | transitive |
| `Microsoft.Extensions.Logging.EventLog` | 10.0.5 | MIT | transitive |
| `Microsoft.Extensions.Logging.EventSource` | 10.0.5 | MIT | transitive |
| `Microsoft.Extensions.Options` | 10.0.5 | MIT | transitive |
| `Microsoft.Extensions.Options` | 10.0.8 | MIT | transitive |
| `Microsoft.Extensions.Options.ConfigurationExtensions` | 10.0.8 | MIT | transitive |
| `Microsoft.Extensions.Primitives` | 10.0.5 | MIT | transitive |
| `Microsoft.Extensions.Primitives` | 10.0.8 | MIT | transitive |
| `Microsoft.OpenApi` | 2.7.5 | MIT | top |
| `Microsoft.VisualStudio.SolutionPersistence` | 1.0.52 | MIT | transitive |
| `Mono.TextTemplating` | 3.0.0 | MIT | transitive |
| `Newtonsoft.Json` | 13.0.3 | MIT | transitive |
| `Npgsql` | 10.0.2 | PostgreSQL | top |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.1 | PostgreSQL | top |
| `PdfPig` | 0.1.14 | Apache-2.0 | top |
| `PDFtoImage` | 4.1.1 | MIT | top |
| `SkiaSharp` | 2.88.9 | MIT | top |
| `SkiaSharp.NativeAssets.Linux.NoDependencies` | 2.88.9 | MIT | top |
| `SkiaSharp.NativeAssets.macOS` | 2.88.9 | MIT | transitive |
| `SkiaSharp.NativeAssets.WebAssembly` | 2.88.8 | MIT † | transitive |
| `SkiaSharp.NativeAssets.Win32` | 2.88.9 | MIT | transitive |
| `Sungaila.PDFium.BlazorWebAssembly` | 131.0.6728 | Apache-2.0 | transitive |
| `System.CodeDom` | 6.0.0 | MIT | transitive |
| `System.Composition` | 9.0.0 | MIT | transitive |
| `System.Composition.AttributedModel` | 9.0.0 | MIT | transitive |
| `System.Composition.Convention` | 9.0.0 | MIT | transitive |
| `System.Composition.Hosting` | 9.0.0 | MIT | transitive |
| `System.Composition.Runtime` | 9.0.0 | MIT | transitive |
| `System.Composition.TypedParts` | 9.0.0 | MIT | transitive |
| `System.Diagnostics.EventLog` | 10.0.5 | MIT | transitive |
| `VersOne.Epub` | 3.3.6 | Unlicense | top |
| `YamlDotNet` | 18.0.0 | MIT | top |

### Test projects only — 17 packages

| Package | Version | License | Top-level / transitive |
|---|---|---|---|
| `coverlet.collector` | 6.0.4 | MIT | top |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.5 | MIT | top |
| `Microsoft.AspNetCore.TestHost` | 10.0.5 | MIT | transitive |
| `Microsoft.CodeCoverage` | 17.14.1 | MIT | transitive |
| `Microsoft.EntityFrameworkCore.InMemory` | 10.0.8 | MIT | top |
| `Microsoft.Extensions.TimeProvider.Testing` | 10.9.0 | MIT | top |
| `Microsoft.NET.Test.Sdk` | 17.14.1 | MIT | top |
| `Microsoft.TestPlatform.ObjectModel` | 17.14.1 | MIT | transitive |
| `Microsoft.TestPlatform.TestHost` | 17.14.1 | MIT | transitive |
| `xunit` | 2.9.3 | Apache-2.0 | top |
| `xunit.abstractions` | 2.0.3 | Apache-2.0 † | transitive |
| `xunit.analyzers` | 1.18.0 | Apache-2.0 | transitive |
| `xunit.assert` | 2.9.3 | Apache-2.0 | transitive |
| `xunit.core` | 2.9.3 | Apache-2.0 | transitive |
| `xunit.extensibility.core` | 2.9.3 | Apache-2.0 | transitive |
| `xunit.extensibility.execution` | 2.9.3 | Apache-2.0 | transitive |
| `xunit.runner.visualstudio` | 3.1.4 | Apache-2.0 | top |

† `xunit.abstractions`: xUnit.net is Apache-2.0 (license URL points to the xunit repo license). `SkiaSharp.NativeAssets.WebAssembly`:
the Microsoft fwlink is SkiaSharp's MIT license. Both resolved from the published URL, not from an SPDX expression.

## External runtime tools (not bundled)

- **FFmpeg / ffprobe** — invoked as separate processes. License depends on the build: the gyan.dev "full" Windows build
  recommended in `docs/nitrate/WINDOWS_HOST.md` is GPL-3.0 (it enables GPL components). Running it as a separate program
  does not relicense Nitrate.
- **PostgreSQL 16** — PostgreSQL License (permissive).

## Reference-only projects (no code used)

Used only as UX/architecture references. **Never copy code from them** (VISION decisions log #2).

| Project | License (per upstream repo; not verified in this repo) |
|---|---|
| Readest — https://github.com/readest/readest | AGPL-3.0 |
| BookOrbit | Not verified — treat as all-rights-reserved; reference only |
| KOReader — https://github.com/koreader/koreader | AGPL-3.0 |

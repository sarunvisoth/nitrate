# Running Nitrate natively on Windows (AMD CPU + GPU)

This guide installs Nitrate directly on the Windows PC. There is no Docker, because Docker/WSL2 cannot use AMD's AMF
hardware encoder (VISION decisions log #4). Everything here is derived from the code; see
[ARCHITECTURE_ANALYSIS.md §19](ARCHITECTURE_ANALYSIS.md#19-native-windows-readiness) for the evidence. **None of it
has been run on Windows yet.** The checklist at the end is for the owner to confirm each step on the real PC.

Commands are PowerShell. Paths such as `C:\Nitrate` and `D:\HomeServer` are examples; change them freely.

## 1. Prerequisites (winget)

```powershell
winget install --id Microsoft.DotNet.SDK.10 -e
winget install --id OpenJS.NodeJS.LTS -e
winget install --id PostgreSQL.PostgreSQL.16 -e
winget install --id Gyan.FFmpeg -e          # the gyan.dev "full" build, which includes h264_amf
winget install --id Git.Git -e
```

If an ID is not found, look it up with `winget search <name>` (e.g. `winget search ffmpeg`) and install the matching
ID. Then open a **new** terminal so PATH updates apply, and enable pnpm:

```powershell
corepack enable
corepack prepare pnpm@10.30.3 --activate
dotnet --version; node --version; pnpm --version
ffmpeg -hide_banner -encoders | findstr amf      # must list h264_amf (also hevc_amf / av1_amf on newer GPUs)
```

If `findstr amf` prints nothing, this ffmpeg cannot use the AMD encoder. Install the gyan.dev *full* build, and point
Nitrate at it with `PRISMEDIA_FFMPEG_PATH` (§5).

The Windows PostgreSQL installer does **not** add its `bin` folder to PATH. Nitrate's backup feature needs `pg_dump`
and `pg_restore`, so either add `C:\Program Files\PostgreSQL\16\bin` to PATH or set the two paths in §5.

## 2. Library folders

```powershell
New-Item -ItemType Directory -Force D:\HomeServer\Movies, D:\HomeServer\TV, D:\HomeServer\Music, `
  D:\HomeServer\Audiobooks, D:\HomeServer\Books, D:\HomeServer\Comics, D:\HomeServer\Photos
```

After first run (§6), add each folder in the web UI: **Settings → Watched Libraries → Browse Folder**. Pick the
folder, then set the scan toggles:

| Folder | Toggles |
|---|---|
| `Movies`, `TV` | Videos |
| `Music` | Audio |
| `Audiobooks`, `Books`, `Comics` | Books (covers `.epub`/`.pdf`, `.cbz`/`.zip` comics, and `.m4b`/`.m4a`/`.mp3` audiobooks) |
| `Photos` | Images |

A new root starts scanning immediately. Unlike the Docker docs, which talk about `/media/...` paths inside the
container, a native install uses the real Windows paths (`D:\HomeServer\Movies`).

## 3. Database

Use the PostgreSQL 16 installed above. The `postgres` superuser password is the one chosen during install.

```powershell
$env:PGPASSWORD = "<postgres superuser password>"
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" -U postgres -h localhost -c "CREATE ROLE nitrate LOGIN PASSWORD '<choose a password>';"
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" -U postgres -h localhost -c "CREATE DATABASE nitrate OWNER nitrate;"
Remove-Item Env:PGPASSWORD
```

The role does not need to be a superuser. The only extension the migrations create is `pg_trgm`, which PostgreSQL 13+
treats as a trusted extension that a database owner may create.

Connection string format (Npgsql key/value). A `postgresql://user:pass@host:5432/db` URL in `DATABASE_URL` also works:

```text
Host=localhost;Port=5432;Database=nitrate;Username=nitrate;Password=<password>
```

The API applies all EF Core migrations on its first start. The worker waits for them.

## 4. Build

```powershell
git clone <your nitrate repo URL> C:\Nitrate\src
cd C:\Nitrate\src
pnpm install --frozen-lockfile
pnpm --filter @prismedia/web-svelte build          # writes apps\web-svelte\build\
dotnet publish apps\backend\src\Prismedia.Api\Prismedia.Api.csproj -c Release -o C:\Nitrate\api
dotnet publish apps\backend\src\Prismedia.Worker\Prismedia.Worker.csproj -c Release -o C:\Nitrate\worker
Remove-Item -Recurse -Force C:\Nitrate\web -ErrorAction SilentlyContinue
Copy-Item -Recurse apps\web-svelte\build C:\Nitrate\web
```

Re-run this section after every `git pull`.

## 5. Configure

Both processes must see the same values. `PRISMEDIA_DATA_DIR` and `PRISMEDIA_CACHE_DIR` in particular **must match**:
the API serves `/assets` from the cache the worker writes, and the worker heartbeat lives in the data dir. Put data
and cache on a fast local drive.

Save this as `C:\Nitrate\nitrate-env.ps1`. Keep it out of the git checkout, because it holds the DB password.

```powershell
$env:ConnectionStrings__Prismedia = "Host=localhost;Port=5432;Database=nitrate;Username=nitrate;Password=<password>"
$env:ASPNETCORE_URLS             = "http://0.0.0.0:8008"      # required: nothing in the code binds 8008 by itself
$env:PRISMEDIA_STATIC_WEB_ROOT   = "C:\Nitrate\web"           # absolute path; a wrong relative path silently serves nothing
$env:PRISMEDIA_DATA_DIR          = "D:\NitrateData"           # default would be C:\data
$env:PRISMEDIA_CACHE_DIR         = "D:\NitrateData\cache"
$env:PRISMEDIA_FFMPEG_PATH       = "C:\path\to\ffmpeg.exe"    # the AMF-capable build; `(Get-Command ffmpeg).Source` shows the PATH one
$env:PRISMEDIA_FFPROBE_PATH      = "C:\path\to\ffprobe.exe"
$env:PRISMEDIA_PG_DUMP_PATH      = "C:\Program Files\PostgreSQL\16\bin\pg_dump.exe"
$env:PRISMEDIA_PG_RESTORE_PATH   = "C:\Program Files\PostgreSQL\16\bin\pg_restore.exe"
```

The remaining variables are optional:

| Variable | Effect |
|---|---|
| `PRISMEDIA_BACKUP_RESTORE_IN_PROCESS` | Nitrate now defaults this to `true` on Windows (a restore runs inside the live API). Leave it unset. |
| `PRISMEDIA_HLS_TRANSCODER` | **Do not rely on it.** The in-app setting (default `Auto`) overrides it. Use §7 instead. |
| `PRISMEDIA_RECOVERY_USERNAME` / `PRISMEDIA_RECOVERY_PASSWORD` | While the password is set, every API start resets the named account (default `admin`) to an enabled admin with that password, creating it if missing. Use it only to regain access, then remove it. |
| `PRISMEDIA_ASSETGEN_THREADS` | ffmpeg threads for background asset generation (default 1). |

Leave `ASPNETCORE_ENVIRONMENT` unset (Production). Development mode proxies the UI to a Vite dev server and turns on CORS.

## 6. Run

Save as `C:\Nitrate\start-nitrate.ps1`:

```powershell
. C:\Nitrate\nitrate-env.ps1
Start-Process -FilePath dotnet -ArgumentList "C:\Nitrate\api\Prismedia.Api.dll"       -WorkingDirectory C:\Nitrate\api
Start-Sleep -Seconds 5
Start-Process -FilePath dotnet -ArgumentList "C:\Nitrate\worker\Prismedia.Worker.dll" -WorkingDirectory C:\Nitrate\worker
```

Run it with `powershell -ExecutionPolicy Bypass -File C:\Nitrate\start-nitrate.ps1`. Then:

1. `Invoke-RestMethod http://localhost:8008/api/health` should return `status : ok`.
2. Open <http://localhost:8008> and complete the first-run wizard, which creates the admin account.
3. Add the libraries (§2).

The worker is **not** supervised: in Docker, the entrypoint restarts it if it crashes, and here nothing does. If jobs
stop progressing, check the worker window. Autostart on boot plus restart-on-failure (Windows Services) is planned
for roadmap milestone M9.

## 7. Enable AMD encoding

In the web UI go to **Settings → HLS Transcoding → HLS transcoder** and choose **AMD AMF**. This saves
`hls.transcoderProfile = Amf` and makes ffmpeg encode HLS with `-c:v h264_amf`. `Auto` never picks AMF on Windows;
it falls back to the CPU encoder (libx264).

Scope of the AMF profile as built: AMF is used for **encoding** only. Decoding stays on the CPU, because the Amf, Nvenc
and Qsv profiles add no hardware-decode flags. HDR sources that need tone mapping use the software path.

## 8. LAN access

Allow the port on **Private** networks only:

```powershell
New-NetFirewallRule -DisplayName "Nitrate" -Direction Inbound -Protocol TCP -LocalPort 8008 -Profile Private -Action Allow
```

Make sure Windows classifies your home network as Private (**Settings → Network & internet → your connection →
Private network**). Then open `http://<PC LAN IP>:8008` (e.g. `http://192.168.x.x:8008`) from the Mac, iPad and
Android phone. Find the IP with `ipconfig`. Do **not** port-forward on the router. Remote access comes later over
Tailscale or WireGuard (M9).

## 9. Known blockers and gaps

Fixed in Nitrate (Task 7):

- A production restore used to stop the API and wait for a supervisor to restart it. The server stayed down and
  the restore never ran. It now runs in-process on Windows.
- The worker heartbeat file replace failed with a sharing violation while the API was reading it, and left `.tmp`
  files behind. The reader now shares write and delete access, and the temp file is cleaned up.

Still open (listed, not fixed):

| Issue | Effect | Workaround |
|---|---|---|
| No process supervision (Docker entrypoint loop has no native equivalent) | A crashed worker stays down; nothing starts Nitrate at boot | Re-run `start-nitrate.ps1`. Windows Services / Task Scheduler are M9 |
| Host-load probe reads `/proc` only | On Windows, background media jobs never back off under CPU or memory pressure (they are still capped by CPU permits) | None needed for light use. A Windows sampler (`GetSystemTimes` + `GlobalMemoryStatusEx`) is a follow-up |
| HLS cache `Directory.Delete` is unguarded (`HlsAssetService.cs`, `ResetStagingDirectory`) | If ffmpeg or a client still holds a segment open, Windows refuses the delete and that playback request fails | Retry playback. The fix needs care, because a failed delete must not leave stale segments mixed into new output |
| Move-mode imports while a download client still has the files open | Move, copy and delete fail on Windows file locks | Use **Hardlink** or **Copy** import mode for acquisitions. Loosening the copy's share mode was rejected, because it could copy a half-written file |
| Import file-name sanitising ignores trailing dots and reserved names (`CON`, `NUL`, `COM1`…) | A folder such as `Vol.` can be stored under a different name than the one Windows writes | Rename such items manually. The fix changes naming on all platforms, so it is deferred |
| Data dir default `/data` | Becomes `C:\data` if `PRISMEDIA_DATA_DIR` is not set | Set it (§5) |
| Stash scrapers need Python and pip packages (installed only in the Docker image) | Stash-compatible scrapers fail to launch | Install Python 3 and the packages listed in `infra/docker/unified.Dockerfile`, or don't use Stash scrapers |
| Update checker compares against Prismedia's Docker images | Update notices are meaningless for Nitrate | Ignore |

**Symlink note.** On Windows, git checks out the `CLAUDE.md → AGENTS.md` symlink as a small text file unless Developer
Mode and `git config core.symlinks true` are both on. This is harmless for running the server.

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

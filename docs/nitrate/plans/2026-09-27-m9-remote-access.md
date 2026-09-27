# M9 — Windows autostart, remote access readiness, Wake-on-LAN

Spec: [VISION.md](../VISION.md) "Networking", "Windows Server Behavior". Roadmap row: M9.
Owner goal: reach Nitrate from outside the home Wi-Fi without toggling a VPN app, without
knowing yet whether the ISP uses CGNAT.

## Part A — Remote-access readiness audit + fixes (Cloud-verifiable)

- [x] Forwarded-headers audit: the API did not call `UseForwardedHeaders`, so
  `HttpContext.Request.IsHttps`/`Scheme`/`Host` and `Connection.RemoteIpAddress` never reflected
  a reverse proxy (Tailscale Serve / cloudflared) terminating TLS on `localhost`. OPDS had grown
  its own ad hoc `X-Forwarded-Proto`/`X-Forwarded-Host` reader that trusted the headers from
  *any* caller, unconditionally. Fixed: added `app.UseForwardedHeaders(...)` as the first
  middleware, trusting only loopback by default (the built-in `ForwardedHeadersOptions` default
  `KnownNetworks`/`KnownProxies`), extendable via `PRISMEDIA_TRUSTED_PROXIES` (comma-separated
  IPs) for setups where the proxy is not on loopback. Removed OPDS's own forwarded-header parsing
  in favor of the framework-processed `request.Scheme`/`request.Host`, so trust is centralized
  and every consumer of `Request.Scheme`/`IsHttps`/`RemoteIpAddress` benefits, not just OPDS.
- [x] Session cookie (`prismedia-session`): already `Secure = context.Request.IsHttps`,
  `HttpOnly`, `SameSite=Lax`. This was already correct in intent, but `IsHttps` was wrong behind
  a proxy until the forwarded-headers fix above; now it reports `true` behind Tailscale
  Serve/cloudflared and `false` on plain-HTTP LAN (`http://192.168.x.x:8008`), unchanged.
- [x] Login brute-force throttle: already existed (`AuthAttemptThrottle`, per-IP+username bucket,
  8 failures / 5 min → 2 min lockout, `429` + `ApiProblemCodes.AuthRateLimited`), and OPDS Basic
  auth already goes through the same throttle (`VerifyCredentialsAsync`). The bug was that the
  bucket's IP component came from `Connection.RemoteIpAddress`, which — without forwarded-headers
  support — is always the reverse proxy's loopback address for every remote client, collapsing
  the per-IP throttle into one shared global bucket. Fixed by the same forwarded-headers change:
  `RemoteIpAddress` is rewritten to the real client IP once a loopback proxy is trusted.
- [x] CORS/WebSocket/SSE/hardcoded `http://` audit: no WebSocket or Server-Sent-Events usage in
  the backend; the dev-only CORS policy only allows `localhost`/`127.0.0.1`/`::1` origins (used
  solely when `ASPNETCORE_ENVIRONMENT=Development`, never in the Windows production run). No
  hardcoded absolute `http://` URLs build client-facing links; HLS playlist segment references
  are relative paths, served same-origin. No share-link/cast feature exists to audit.
- [x] Tests (`Prismedia.Api.Tests`): forwarded headers honoured from loopback, ignored from a
  non-trusted remote address; cookie `Secure` flag on/off behind trusted HTTPS proxy vs. plain
  LAN HTTP; throttle bucket keyed by the real forwarded client IP (two distinct forwarded IPs
  each get their own attempt budget behind one trusted proxy address); OPDS absolute links keep
  using forwarded origin only when the caller is a trusted proxy.

## Part B — Windows scripts (Owner-verified)

- [x] `scripts/windows/install-autostart.ps1`: registers two Scheduled Tasks (`Nitrate-Api`,
  `Nitrate-Worker`), trigger `AtStartup`, `-RunLevel Highest`, configurable run-as account
  (default: task runs as `SYSTEM`), restart-on-failure (3 restarts, 1 minute apart), working
  directory `C:\Nitrate`, loads `nitrate-env.ps1` before launching `dotnet`. Idempotent
  (`Unregister-ScheduledTask` first if present). Also sets the `postgresql-x64-16` service
  `StartupType` to `Automatic`.
- [x] `scripts/windows/uninstall-autostart.ps1`: unregisters both tasks; leaves PostgreSQL's
  startup type alone (owner may use Postgres for other things).
- [x] Wake-on-LAN documented (not scripted — needs BIOS/router changes a script cannot make).
- [x] Parse-checked with `pwsh` (installed via `dotnet tool`) using
  `[System.Management.Automation.Language.Parser]::ParseFile`; never claimed run on Windows.

## Part C — Docs

- [x] `docs/nitrate/REMOTE_ACCESS.md`: CGNAT check, Tailscale (recommended), Cloudflare Tunnel,
  port-forward+Caddy fallback, Wake-on-LAN, security checklist. Linked from `WINDOWS_HOST.md`.
- [x] `WINDOWS_HOST.md`: new "Autostart on boot" section (§10) pointing at the scripts; owner
  checklist extended with autostart/remote-access items.

## Files

- `apps/backend/src/Prismedia.Api/Program.cs` — `UseForwardedHeaders`.
- `apps/backend/src/Prismedia.Api/Security/ForwardedHeadersSetup.cs` — new, options builder.
- `apps/backend/src/Prismedia.Api/Endpoints/Opds/OpdsEndpoints.cs` — drop manual forwarded parsing.
- `apps/backend/tests/Prismedia.Api.Tests/ForwardedHeadersTests.cs` — new.
- `apps/backend/tests/Prismedia.Api.Tests/OpdsEndpointTests.cs` — trust-boundary cases.
- `scripts/windows/install-autostart.ps1`, `uninstall-autostart.ps1` — new.
- `docs/nitrate/REMOTE_ACCESS.md` — new. `docs/nitrate/WINDOWS_HOST.md` — updated.
- `docs/nitrate/ROADMAP.md` — status update.

## Tests

- `pnpm test:backend` (`dotnet test` across the backend solution).
- `pnpm check`, `pnpm api:check`.
- PowerShell scripts: `pwsh -Command "[System.Management.Automation.Language.Parser]::ParseFile('...', [ref]$null, [ref]$null)"` — parse only.

## Owner-verified checklist (cannot be done from the cloud sandbox)

- [ ] Run `install-autostart.ps1` as Administrator; reboot the PC; confirm both tasks start the
  API and worker without logging in, and `http://localhost:8008/api/health` responds.
- [ ] Kill the API process; confirm Task Scheduler restarts it (check Task Scheduler history).
- [ ] Install Tailscale on the PC and at least one other device; run
  `tailscale serve --bg 8008`; confirm `https://<pc>.<tailnet>.ts.net` works from outside the
  home Wi-Fi (phone on cellular data).
- [ ] Turn on iOS "Connect on Demand" / Android always-on VPN for Tailscale so it never needs
  manual toggling.
- [ ] Confirm the closed-source Prismedia iPhone/iPad/Apple TV apps still connect over the new
  URL (or over LAN, unaffected).
- [ ] Decide CGNAT status (WAN IP vs. `whatismyip.com`) and pick Tailscale vs. Cloudflare Tunnel
  vs. port-forward accordingly.
- [ ] If choosing Cloudflare Tunnel: verify Cloudflare Access PIN gate, and confirm whether the
  Apple apps/OPDS readers still work through Access (may need a bypass policy).
- [ ] BIOS/NIC Wake-on-LAN enabled; Windows "Fast startup" disabled; confirm a magic packet from
  another LAN device wakes the PC.
- [ ] Confirm login throttle in practice: repeated wrong passwords from a phone eventually show
  "Too many failed attempts" without locking out the legitimate device on Wi-Fi at the same time.

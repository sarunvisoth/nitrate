# Remote access: reaching Nitrate from outside your home Wi-Fi

This assumes you followed [WINDOWS_HOST.md](WINDOWS_HOST.md) and Nitrate already works at
`http://<PC LAN IP>:8008` from devices on your home network. None of this has been run yet —
the checklist at the end is for you to confirm on the real PC and your own devices.

## 1. Check whether you're behind CGNAT

Your home internet may share one public IP address across many customers (Carrier-Grade NAT,
"CGNAT"). If so, port-forwarding on your router (Option 3 below) will not work, no matter how
you configure it — the traffic never reaches your router at all.

1. On the PC (or any device on your Wi-Fi), open your router's status page and note the
   "WAN IP" / "Internet IP" it shows. Router admin pages are usually `http://192.168.1.1` or
   `http://192.168.0.1` — check the sticker on the router if unsure.
2. From the same network, visit <https://www.whatismyip.com> in a browser.
3. Compare the two numbers.
   - **Same number** → you have a real public IP. All three options below work; Option 3
     (port-forward) becomes viable.
   - **Different number**, or the router's WAN IP falls in `100.64.0.0` – `100.127.255.255`
     (i.e. starts with `100.64` through `100.127`) → you are behind CGNAT. Port-forwarding
     cannot work. Use **Option 1 (Tailscale)** or **Option 2 (Cloudflare Tunnel)** — both work
     fine behind CGNAT because the PC makes an outbound connection to them; nothing needs to
     reach your router from the internet.

If you are not sure, Tailscale (Option 1) is the safest choice regardless of CGNAT status.

## 2. Option 1 (recommended): Tailscale

Tailscale creates a private mesh network (a WireGuard VPN under the hood) between your devices,
and `tailscale serve` puts a real HTTPS certificate in front of Nitrate. It works whether or not
you have CGNAT, and does not need you to open anything on your router.

**On the Windows PC:**

1. Install from <https://tailscale.com/download/windows>, sign in (free personal plan covers
   this use case — up to 100 devices, 3 users).
2. In the Tailscale admin console (<https://login.tailscale.com/admin/machines>), enable
   **MagicDNS** and **HTTPS Certificates** under *DNS* settings. This is what lets
   `tailscale serve` issue a real, browser-trusted certificate instead of a self-signed one.
3. Run once, in an elevated PowerShell:
   ```powershell
   tailscale serve --bg 8008
   ```
   This proxies `https://<pc-name>.<your-tailnet>.ts.net` (port 443, standard HTTPS, no port
   number to type) to `http://localhost:8008`. `--bg` makes it persist across Tailscale
   restarts; run `tailscale serve status` to confirm, and `tailscale serve --https=443 off` to
   undo it.
4. Because `tailscale serve` connects to Nitrate from the same machine (`localhost`), your
   existing firewall rule (Private profile, port 8008) needs **no change** — the connection
   never leaves the loopback interface. Nitrate's forwarded-headers handling (M9) already
   trusts this loopback proxy and sets the session cookie's `Secure` flag correctly for the
   `https://...ts.net` address.

**On each device you want to use remotely** (iPhone, iPad, Android, Mac, another PC):

1. Install the Tailscale app and sign in with the same account (or accept an invite — see
   "Sharing with family" below).
2. Open `https://<pc-name>.<your-tailnet>.ts.net` in a browser, or give that URL to the
   closed-source Prismedia app if it accepts a custom server address.
3. **Avoid the toggle-it-on-and-off annoyance:**
   - **iOS/iPadOS:** Settings → VPN → tap the Tailscale VPN → turn on **"Connect On Demand"**.
     Tailscale then connects automatically whenever you leave home Wi-Fi and disconnects when
     you're back on it, with nothing to tap.
   - **Android:** Settings → Network & internet → VPN → Tailscale (gear icon) → enable
     **"Always-on VPN"**. Optionally also enable "Block connections without VPN" if you want
     Nitrate access to fail closed rather than silently fall back to no connection.
   - Tailscale uses **split tunneling** by default — only traffic addressed to another
     Tailscale device (your PC) goes through the tunnel. Regular browsing, other apps, and
     battery life are unaffected; this is not a traditional "route everything through a VPN"
     setup.

**Direct connection vs. relay (DERP):** Tailscale tries to connect devices directly
(peer-to-peer); if that fails (strict NAT/firewall on either end), it falls back to Tailscale's
relay servers ("DERP"), which works everywhere but is slower and adds latency. Check which one
you're getting:
```powershell
tailscale ping <other-device-name>
```
A direct connection reports the real round-trip IP; a relayed one says
`via DERP(<region>)`. **Caveat:** if your connection ends up relayed, high-bitrate 4K video or
hi-res FLAC playback may stutter — Tailscale's free relay bandwidth is shared and not built for
sustained large-file streaming. Direct connections are typically fine for anything Nitrate
serves. If you're stuck on relay, port-forwarding UDP 41641 on your router (if not CGNAT) or
trying from a different network usually restores a direct path.

**Sharing with family:** in the admin console, either invite them as **Tailscale users** (each
gets a login and appears as their own node — good for people who'll access other services too)
or **share just this one machine** with them (*Machines* → your PC → *Share* → enter their
email; they see only this device, not your whole tailnet). Either way they follow the "each
device" steps above.

## 3. Option 2 (no app to install on devices): Cloudflare Tunnel

If you don't want family members installing an app, and you own a domain (or are willing to buy
one, a few dollars a year), Cloudflare Tunnel exposes Nitrate at a normal-looking URL
(`https://nitrate.example.com`) without opening any port. It also works behind CGNAT, because
`cloudflared` on the PC makes the only outbound connection.

1. Add your domain to Cloudflare (free plan) and point its nameservers there.
2. Install `cloudflared` on the PC: <https://github.com/cloudflare/cloudflared/releases> (the
   Windows `.msi`), then register it as a Windows service so it survives reboots and doesn't
   need a signed-in user — same idea as `install-autostart.ps1` but for `cloudflared` itself:
   ```powershell
   cloudflared service install <your-tunnel-token>
   ```
   (the token comes from the Cloudflare Zero Trust dashboard → *Networks* → *Tunnels* → *Create
   a tunnel* → *Connector* step.)
3. In the tunnel's **Public Hostname** settings, add `nitrate.example.com` → Service type
   `HTTP` → URL `localhost:8008`.
4. **Strongly recommended:** put **Cloudflare Access** in front of it (Zero Trust dashboard →
   *Access* → *Applications* → add the same hostname). Configure a policy that emails a
   one-time PIN to your and your family's addresses before Cloudflare will even proxy the
   request through to Nitrate — this adds a second login layer in front of Nitrate's own
   account system, which matters because the hostname is public DNS.

**Caveats:**

- Cloudflare's terms of service discourage serving large amounts of video/media through their
  free tier CDN/tunnel — it's built for web traffic, not as a video CDN. Books, comics, and
  music are fine; treat heavy 4K video streaming as a reason to prefer Tailscale instead.
- The closed-source Prismedia iPhone/iPad/Apple TV apps and any OPDS e-reader app authenticate
  with Nitrate directly (Basic auth / bearer tokens) — they generally cannot complete
  Cloudflare Access's browser-based PIN flow. Two ways to handle this:
  - Add a **bypass policy** in Access scoped to those specific request paths (e.g. `/opds/*`,
    or the native API paths those apps use) so Access lets them straight through to Nitrate,
    which still requires its own login. This narrows Access's protection to the web UI only —
    acceptable since the native/OPDS paths still need a real Nitrate account, just not the
    extra PIN step.
  - Or skip Access for those apps and only use it in front of the web UI's own hostname.

## 4. Option 3 (only if you are *not* behind CGNAT): port-forward + Caddy

The traditional approach: forward a port on your router straight to the PC. Only works if
section 1 confirmed you have a real public IP. **This is the option that most exposes the
server to the internet** — do this last, and only if Tailscale/Cloudflare don't fit.

1. Get a DDNS hostname (many routers have built-in DDNS support for a free provider like
   DuckDNS or No-IP), since home IPs usually change.
2. Install [Caddy](https://caddyserver.com/) on the PC as a Windows service. A `Caddyfile` this
   simple gets automatic HTTPS (Caddy requests and renews the certificate itself via Let's
   Encrypt) with zero manual certificate handling:
   ```
   nitrate.yourdomain.example {
       reverse_proxy localhost:8008
   }
   ```
3. Forward **port 443** on your router to the PC's LAN IP, port 443 (Caddy listens there, not
   8008). **Never forward port 8008 directly** — always go through Caddy (or another
   TLS-terminating proxy) so the connection is encrypted end to end and Nitrate sees it as an
   HTTPS request from loopback, the same trust boundary as Options 1 and 2.

**Warning:** this puts Nitrate's login page on the open internet. Anyone who finds the hostname
(scanners do find things like this) can attempt to log in. Nitrate's login throttle (M9: 8
failed attempts from the same IP+username lock out for 2 minutes, HTTP 429) blunts brute-force
attempts, but a strong, unique admin password matters far more here than with Options 1-2, where
the VPN/tunnel is the first line of defense. Keep the router's admin UI on a different port than
443/8008 and never expose it.

## 5. Wake-on-LAN: waking the PC when it's asleep or off

Nitrate's library is only reachable while the PC is on. To let it sleep and still be reachable:

1. **BIOS/UEFI:** enable "Wake on LAN" / "Power On by PCI-E/PCI" (naming varies by
   motherboard) in the BIOS setup screen.
2. **Network adapter (Windows):** Device Manager → Network adapters → your adapter →
   Properties → *Power Management* tab → check **"Allow this device to wake the computer"**;
   *Advanced* tab → set **"Wake on Magic Packet"** to `Enabled`.
3. **Windows "Fast Startup" caveat:** Fast Startup (Control Panel → Power Options → "Choose
   what the power buttons do" → "Change settings that are currently unavailable") hibernates
   the kernel session on shutdown rather than fully powering off, which can prevent the network
   adapter from waking the PC from a true "off" state. If WoL from a full shutdown doesn't work,
   disable Fast Startup and only rely on Sleep (not Shutdown) for this PC, or test both.
4. **Don't let the PC sleep while it's meant to be hosting:** Settings → System → Power & Sleep,
   set "Screen"/"Sleep" to *Never* while plugged in, or at least keep Sleep off if you want
   Nitrate reachable at all times without a magic packet first. A screen-off, CPU-awake state is
   fine and saves power; full Sleep needs waking first.
5. **Sending the magic packet remotely is the hard part.** A magic packet only works on the
   *local* network segment — you can't send one directly from the internet to a sleeping PC.
   You need something on the LAN that's always awake to relay it:
   - Many routers have a built-in WoL page you can reach through their own admin app (if it
     supports remote access) — check your router's manual.
   - A cheap always-on device on the LAN (a Raspberry Pi, or even your Tailscale-connected
     router) running as a Tailscale **subnet router** lets you `tailscale up` from anywhere and
     send the magic packet to the PC's LAN broadcast address from that device.
   - A phone app (e.g. "Wake On Lan" on iOS/Android) works only while your phone itself is on
     the same Wi-Fi — useful for "wake it before I leave home," not for waking it once you're
     already remote.
   - Simplest in practice for a single-owner setup: don't rely on WoL for remote wake at all —
     just leave the PC on (screen off, not asleep) whenever you might want remote access, and
     treat WoL as a LAN-only convenience (e.g. a phone shortcut you tap before leaving the
     house).

## 6. Security checklist

- [ ] Admin account uses a strong, unique password (not reused from another site).
- [ ] Every family member has their own account rather than sharing the admin login (Settings →
  Users).
- [ ] Windows is set to install updates automatically.
- [ ] Port 8008 (and any raw HTTP port) stays **closed** on the router — always go through
  Tailscale, Cloudflare Tunnel, or a TLS-terminating proxy (Option 3), never a bare
  `http://` port-forward.
- [ ] You understand what the login throttle does: 8 failed attempts for the same
  username+IP within 5 minutes locks that combination out for 2 minutes (HTTP 429), for both
  the web login and OPDS Basic auth. It slows brute-forcing; it does not replace a strong
  password.
- [ ] If using Cloudflare Tunnel, Access (PIN gate) is enabled on the hostname, with an explicit
  bypass only for the specific native/OPDS paths that need it (§3).
- [ ] You know which option you chose and why (CGNAT status from §1 drives this).

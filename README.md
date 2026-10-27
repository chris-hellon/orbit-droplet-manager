# Orbit

A self-hosted, visual control room for your DigitalOcean droplets. Orbit renders your fleet as
glowing markers on a 3D globe, then lets you **click a droplet and manage everything inside it** —
the websites it serves, its services, databases, firewall, logs, and a full SSH terminal — from one
dashboard.

Built as a single **.NET 10 Blazor Web App** (WebAssembly interactivity) that ships as one Docker image.

> ⚠️ Early but functional. Built for personal use first; open to others later. Expect rough edges.

---

## What it does

### Dashboard
- A **3D globe** (globe.gl) with a pulsing marker per droplet — colour = health (green / amber / red / grey),
  and the camera centres on your fleet. Hover for a tooltip, click to open a droplet.
- **Stat tiles** — droplets, healthy, needing attention, monthly cost, regions.
- **Alerts** — automatically surfaces droplets that are critical, in a warning state, or missing
  monitoring data.
- **Activity feed** — every action Orbit takes (power, snapshot, resize, rebuild, service restart,
  add site, enable HTTPS, firewall changes, database create/drop) is logged with a timestamp.
- A plain **list view** toggle for dense scanning.

### Fleet management (DigitalOcean API)
- Connect a DO personal access token (encrypted at rest), list droplets with specs / IPs / tags / region / price.
- Live **CPU / RAM / disk** from the DO monitoring API, streamed to the browser over SignalR.
- **Power** actions (on / off / reboot) with confirmation on destructive ones, **snapshots**,
  and **resize** / **rebuild** (from a distribution or one of your snapshots).

### Inside the droplet (over SSH)
Click a droplet → **Manage**, and you get tabs for:
- **Websites** — detects nginx sites (`nginx -T`): domains, reverse-proxy target, SSL status + expiry.
  Add a new site (reverse proxy or static), enable HTTPS via certbot, and manage the site's DNS when the
  zone is hosted on DigitalOcean (including a one-click "point this domain at the droplet").
- **Firewall** — manage `ufw`: view/add/delete rules, enable/disable (SSH is auto-allowed so you can't
  lock yourself out).
- **Database** — detects an on-box MySQL/MariaDB or PostgreSQL server; list/create/drop databases and users
  (with a generated password shown once).
- **Services** — systemd units, filtered to *your* app/web/db services by default; start / stop / restart,
  and per-service `journalctl` logs.
- **Logs** — tail nginx access/error, syslog, or a service's journal.
- **Terminal** — a full interactive **SSH shell** in the browser (xterm.js).

---

## Architecture

| Project | Role |
| --- | --- |
| `src/Orbit` | ASP.NET Core host: minimal-API endpoints, SignalR hubs (live metrics + SSH terminal), Blazor Web App host. |
| `src/Orbit.Client` | Blazor WebAssembly UI — dashboard/globe, the per-droplet management tabs, globe.gl + xterm.js interop. |
| `src/Orbit.Core` | Domain: `IDropletProvider` + `DigitalOceanProvider`, SSH services (SSH.NET), EF Core (SQLite), encrypted credentials, the metrics-poller background service, activity log. |
| `src/Orbit.Shared` | DTOs, enums, route + hub constants shared by client and server. |

Key design points:
- **Provider abstraction.** `IDropletProvider` keeps DigitalOcean behind an interface so Hetzner / Linode
  could be added by writing one more implementation.
- **Encryption at rest.** The DO token and the SSH private key are encrypted with ASP.NET Core Data
  Protection before they touch the database — only ciphertext is stored.
- **Multi-tenant from day one.** `Workspace` and `User` exist in the schema (one seeded user today) so
  multi-user can be added without a schema rewrite.
- **Command safety.** Everything run over SSH is validated (hostnames, ports, unit names, DB identifiers,
  file paths) before interpolation, and the droplet's host is resolved server-side — never trusted from the client.

---

## Run locally

Requires the **.NET 10 SDK**.

```bash
dotnet run --project src/Orbit --launch-profile http
```

Open <http://localhost:5162> → **Settings**:
1. Paste a DigitalOcean [personal access token](https://cloud.digitalocean.com/account/api/tokens)
   (read & write scope). Your droplets appear on the globe immediately.
2. To manage the inside of your droplets, add an **SSH private key** (whatever key already has access to them).

In development the SQLite database and Data-Protection keys are written to `src/Orbit/.data/` (git-ignored).
Delete that folder to reset.

---

## Run with Docker

```bash
docker build -t orbit .
docker run -d --name orbit -p 8080:8080 -v orbit-data:/data orbit
```

Open <http://localhost:8080>. The `/data` volume holds the SQLite database **and** the Data-Protection key
ring — keep it, or previously-stored credentials can't be decrypted after a restart.

### Configuration

Everything is environment-variable driven (double-underscore binds nested keys):

| Variable | Default | Purpose |
| --- | --- | --- |
| `Orbit__DataDirectory` | `/data` | Where the SQLite db + DP keys live. |
| `Orbit__DatabaseFileName` | `orbit.db` | Database file name. |
| `Orbit__MetricsPollSeconds` | `60` | Seconds between metric polls. |
| `ASPNETCORE_URLS` | `http://+:8080` | Listen address. |

---

## Security notes

- The DO token and SSH key are encrypted at rest with ASP.NET Core Data Protection. The DP keys live in the
  `/data` volume **unencrypted on disk** (normal on Linux/containers — there's no DPAPI), so protect that
  volume with filesystem permissions.
- SSH host-key verification is not yet enforced — intended for managing your *own* droplets on a trusted network.
- This is a management tool for infrastructure you own. Don't expose it publicly without putting authentication
  in front of it (there is no built-in login yet — single-user, local/trusted use for now).

---

## Tech stack

.NET 10 · Blazor WebAssembly · ASP.NET Core minimal APIs · SignalR · EF Core + SQLite ·
[SSH.NET](https://github.com/sshnet/SSH.NET) · [globe.gl](https://github.com/vasturiano/globe.gl) ·
[xterm.js](https://xtermjs.org/).

---

## Roadmap

Backups/snapshots surfaced per droplet, scheduled tasks (cron), bulk actions across droplets,
historical metric graphs, authentication/multi-user, SSH host-key pinning, and offline-vendored
globe/xterm assets.

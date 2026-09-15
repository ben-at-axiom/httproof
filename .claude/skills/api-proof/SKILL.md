---
name: api-proof
description: |
  Produce a PNG screenshot as evidence that an HTTP API endpoint behaves as expected —
  the URL, request state, and rendered response visible in one image. Uses HTTProof, a
  local Avalonia REST client (Windows) driven via its 127.0.0.1 REST API. Invoke when the
  user asks for "proof", "evidence", or a screenshot of an API call; when demonstrating
  that a fix actually returns the right response; or when a PR/ticket needs a visual
  artifact of a live request. Not for load testing, contract testing, or automated
  assertions — a single request, single screenshot.
allowed-tools:
  - PowerShell
  - Read
  - Write
---

# API Proof (HTTProof)

Single-request screenshot workflow: launch HTTProof, drive one request through
its local API, grab a PNG of the window, shut down.

## Platform

Windows only. HTTProof is a WPF app; the driving API binds `127.0.0.1` (IPv4) only.
**Use `127.0.0.1` in every request URL — `localhost` can resolve to `::1` and fail.**

## Recipe

### 1. Launch HTTProof with a known port

```powershell
$out = "$env:TEMP\httproof.out"
$err = "$env:TEMP\httproof.err"
$p = Start-Process -FilePath "httproof" `
    -ArgumentList "--port","5959" `
    -RedirectStandardOutput $out -RedirectStandardError $err -PassThru
$p.Id  # keep for cleanup
```

Passing `--port` explicitly is important — if `5959` is in use, HTTProof falls back
to an ephemeral port and you must parse `$out` (first line is `{"server":"http://127.0.0.1:<port>"}`)
to learn the real one. Pick an unused port up front to skip that.

### 2. Wait for the server to bind

```powershell
$base = "http://127.0.0.1:5959"
$deadline = (Get-Date).AddSeconds(10)
do {
  try { Invoke-WebRequest "$base/api/health" -UseBasicParsing -TimeoutSec 2 | Out-Null; break }
  catch { Start-Sleep -Milliseconds 300 }
} while ((Get-Date) -lt $deadline)
```

### 3. Drive the request

Two forms — pick one:

**A. One-shot: POST /api/send with the full patch in the body**

```powershell
$patch = @{
  method = "GET"
  url    = "https://api.example.com/things/42"
  headers = @(@{key="Accept"; value="application/json"})
  auth = @{ kind = "bearer"; secret = $env:MY_TOKEN }
} | ConvertTo-Json -Depth 5 -Compress

Invoke-WebRequest "$base/api/send" -Method POST -Body $patch `
    -ContentType "application/json" -UseBasicParsing | Out-Null
```

**B. Two-step: PUT/PATCH /api/state, then POST /api/send with empty body**

Use when you want to prep the request without firing it — e.g., let the user eyeball
the loaded state first (rare for a scripted proof; usually A).

Body shape (`RequestPatch`):

```jsonc
{
  "method": "GET",
  "url": "https://...",
  "params":  [{"key": "...", "value": "..."}],
  "headers": [{"key": "...", "value": "..."}],
  "auth":  { "kind": "none|basic|bearer", "user": "...", "secret": "..." },
  "body":  { "kind": "none|json|text",    "text": "..." }
}
```

### 4. Screenshot

```powershell
$png = "<destination>.png"  # ask user where, or drop into ./capture/
Invoke-WebRequest "$base/api/screenshot" -OutFile $png -UseBasicParsing
```

Then Read the PNG so it renders in the conversation.

### 5. Shut down

```powershell
try { Invoke-WebRequest "$base/api/exit" -Method POST -UseBasicParsing -TimeoutSec 3 | Out-Null } catch {}
```

Belt-and-braces: `Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue`.

## Where to save the PNG

- Ephemeral evidence pasted into a chat / PR comment: `$env:TEMP` is fine.
- Committed alongside a ticket: ask the user where (`capture/`, `docs/proof/`,
  project-specific). Never commit without asking.

## Driving API reference

Fetch the full OpenAPI doc from `GET /` (root) any time — it's the authoritative
spec. Summary:

| Route              | Method     | Purpose                                    |
|--------------------|------------|--------------------------------------------|
| `/`                | GET        | OpenAPI 3 JSON (this doc)                  |
| `/api/health`      | GET        | Liveness probe                             |
| `/api/state`       | GET        | Current request+response state             |
| `/api/state`       | PUT        | Overwrite request fields                   |
| `/api/state`       | PATCH      | Merge request fields                       |
| `/api/send`        | POST       | Fire (optional patch in body)              |
| `/api/screenshot`  | GET        | PNG of the main window                     |
| `/api/exit`        | POST       | Shut down                                  |

## Gotchas

- **`localhost` vs `127.0.0.1`** — the server binds IPv4 only; `localhost` on Windows
  may hit IPv6 first and time out. Always `127.0.0.1`.
- **Running `httproof` inline from Bash `run_in_background`** — it's a Windows GUI
  process; the Bash wrapper may exit non-zero even when the app launches. Use
  PowerShell `Start-Process -PassThru` with redirected stdio.
- **Port collision** — always pass `--port <n>`; parse the `{"server":...}` line
  from stdout only if you must accept the ephemeral fallback.
- **Secrets in requests** — never write bearer tokens or basic-auth passwords into
  the transcript. Read from env vars (`$env:...`) and pass through. The screenshot
  will show the response body; if that includes sensitive fields, warn the user
  before saving anywhere durable.

## Never

- Never use this for automated assertions — it produces one image, not a pass/fail.
- Never leave HTTProof running after the screenshot; always call `/api/exit`.
- Never commit the PNG without the user's say-so.

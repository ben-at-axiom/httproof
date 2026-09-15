# HTTProof

Screenshottable REST client for Windows, driveable by CLI args and a local HTTP API.

Purpose: give an agentic LLM (or a human) a REST client that:

- Looks professional.
- Can be started with a fully-prepared request via CLI args, so the agent doesn't have to click through the UI.
- Exposes a small local HTTP API so anything can drive the UI: change the request, hit Send, read the response.

See [.claude/skills/api-proof/SKILL.md](./.claude/skills/api-proof/SKILL.md) for an example Claude skill that tells Claude when and how to invoke this tool. (Skill assumes `HTTProof.exe` can be found via `$env:PATH`.)

Stack: Avalonia + .NET 10, dark theme only, JSON syntax highlighting via AvaloniaEdit + TextMate.

## Layout

- Top bar: method dropdown, URL, Send / Cancel.
- Left pane: tabs for Params, Headers, Auth, Body.
- Right pane: status badge (colored by class), duration, byte count, content type, JSON-highlighted response body.

## Running

```
dotnet run --project HTTProof
```

On start, it prints the driving server URL to stdout as JSON:

```json
{
  "server": "http://127.0.0.1:5959"
}
```

## CLI

```
HTTProof [options]

--method, -X <VERB>          HTTP method (default GET)
--url, -u <URL>              Request URL
--param, -p <k=v>            Query param (repeatable)
--header, -H <k:v>           Header (repeatable)
--auth-basic <user:pass>     Basic auth
--auth-bearer <token>        Bearer token
--body-json <json|@file>     JSON body (prefix @ for file)
--body-text <text|@file>     Text body
--send                       Send immediately after prep; keep window open
--port <n>                   Bind driving server to this port (default: 5959; falls back to ephemeral if in use)
--help, -h
```

### Insta-send from CLI

Open the app with a prepared request, send it, print the JSON result to stdout, keep the window open for screenshotting:

```
HTTProof --send \
  --method POST \
  --url http://127.0.0.1:8000/api/graphql \
  --header Content-Type:application/json \
  --body-json '{"query":"{__typename}"}'
```

Stdout gets three JSON lines: the server URL, an ack, and the result.

## Driving API

Once the app is running, the server hosts:

```
GET  /api/health            { "ok": true }
GET  /api/state             full request + response state
PUT  /api/state             patch request fields (partial JSON body)
POST /api/send              send now (optional patch body); returns state DTO
GET  /api/screenshot        PNG bytes of the main window
POST /api/exit              close the app
```

Response DTO shape:

```jsonc
{
  "request": {
    "method": "POST",
    "url": "...",
    "params":  [{ "enabled": true, "key": "", "value": "" }],
    "headers": [{ "enabled": true, "key": "", "value": "" }],
    "auth":    { "kind": "none|basic|bearer", "user": "", "secret": "" },
    "body":    { "kind": "none|json|text", "text": "" }
  },
  "response": {
    "status": 200,
    "statusText": "OK",
    "headers":  { "Content-Type": "application/json", ... },
    "body": "...",
    "contentType": "application/json",
    "durationMs": 480,
    "byteLength": 202
  },
  "error": null,
  "isBusy": false,
  "hasResponse": true
}
```

Note: HttpListener requires POST requests to carry a `Content-Length` header — send `-d '{}'` or `-H 'Content-Length: 0'` when POSTing an empty body.

## Screenshot flow (agent recipe)

1. Start HTTProof with the prepared request and `--send`.

2. Wait for send to complete before screenshotting, otherwise the UI still shows a spinner. Pick one:

   - Read stdout and wait for the third JSON line (the result) — `--send` prints it after the request finishes.
   - Poll `GET /api/state` until `isBusy` is `false` and `hasResponse` is `true`.
   - Skip `--send` and drive the send over HTTP with `POST /api/send` — the call blocks until the request finishes and returns the state DTO.

3. Fetch the window PNG from the driving server:

   ```
   curl -o shot.png http://127.0.0.1:5959/api/screenshot
   ```


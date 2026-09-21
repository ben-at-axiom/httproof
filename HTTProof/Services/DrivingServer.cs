using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using HTTProof.ViewModels;

namespace HTTProof.Services;

public sealed class DrivingServer : IDisposable
{
    private readonly MainViewModel _vm;
    private HttpListener _listener = new();
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public int Port { get; private set; }
    public string BaseUrl => $"http://127.0.0.1:{Port}";

    public static JsonSerializerOptions JsonOpts { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public DrivingServer(MainViewModel vm)
    {
        _vm = vm;
    }

    public void Start(int port = 0)
    {
        if (port == 0) port = PickFreePort();
        if (!TryBind(port) && !TryBind(PickFreePort()))
            throw new InvalidOperationException("could not bind any port for driving server");
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => LoopAsync(_cts.Token));
    }

    private bool TryBind(int port)
    {
        // HttpListener disposes itself on a failed Start(), so use a fresh instance per attempt.
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        try
        {
            listener.Start();
            _listener = listener;
            Port = port;
            return true;
        }
        catch (HttpListenerException)
        {
            try { listener.Close(); } catch { }
            return false;
        }
        catch (SocketException)
        {
            try { listener.Close(); } catch { }
            return false;
        }
    }

    private static int PickFreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync().ConfigureAwait(false); }
            catch (HttpListenerException) { break; }
            catch (ObjectDisposedException) { break; }

            _ = Task.Run(() => HandleAsync(ctx), ct);
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        try
        {
            var req = ctx.Request;
            var path = req.Url?.AbsolutePath ?? "/";
            var method = req.HttpMethod.ToUpperInvariant();

            switch (path, method)
            {
                case ("/", "GET"):
                    await WriteJsonRaw(ctx, 200, OpenApiSpec());
                    return;

                case ("/api/screenshot", "GET"):
                    {
                        var pathQ = req.QueryString["path"];
                        var (png, err) = await CaptureScreenshotAsync(pathQ);
                        if (err is not null) { await WriteError(ctx, 400, err); return; }
                        await WriteBytes(ctx, 200, "image/png", png!);
                        return;
                    }

                case ("/api/state", "GET"):
                    await WriteJson(ctx, 200, await UiAsync(() => StateMapper.ToDto(_vm)));
                    return;

                case ("/api/state", "PUT"):
                case ("/api/state", "PATCH"):
                    {
                        var patch = await ReadJson<RequestDto>(req);
                        if (patch is null) { await WriteError(ctx, 400, "empty body"); return; }
                        await UiAsync(() => { StateMapper.ApplyPatch(_vm, patch); return 0; });
                        await WriteJson(ctx, 200, await UiAsync(() => StateMapper.ToDto(_vm)));
                        return;
                    }

                case ("/api/send", "POST"):
                    {
                        var patch = req.HasEntityBody ? await ReadJson<RequestDto>(req) : null;
                        if (patch is not null)
                            await UiAsync(() => { StateMapper.ApplyPatch(_vm, patch); return 0; });
                        var result = await UiAsync(_vm.ExecuteAsync);
                        var dto = await UiAsync(() => StateMapper.ToDto(_vm));
                        var status = result.Response is not null ? 200 : 502;
                        await WriteJson(ctx, status, dto);
                        return;
                    }

                case ("/api/exit", "POST"):
                    await WriteJson(ctx, 200, new { ok = true });
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(50);
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime d)
                                d.Shutdown();
                        });
                    });
                    return;

                case ("/api/health", "GET"):
                    await WriteJson(ctx, 200, new { ok = true });
                    return;

                default:
                    await WriteError(ctx, 404, $"no route for {method} {path}");
                    return;
            }
        }
        catch (Exception ex)
        {
            try { await WriteError(ctx, 500, ex.Message); } catch { }
        }
    }

    private static async Task<T?> ReadJson<T>(HttpListenerRequest req) where T : class
    {
        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
        var text = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(text)) return null;
        return JsonSerializer.Deserialize<T>(text, JsonOpts);
    }

    private static async Task WriteJson(HttpListenerContext ctx, int status, object payload)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        var json = JsonSerializer.Serialize(payload, JsonOpts);
        var bytes = Encoding.UTF8.GetBytes(json);
        ctx.Response.ContentLength64 = bytes.LongLength;
        await ctx.Response.OutputStream.WriteAsync(bytes);
        ctx.Response.OutputStream.Close();
    }

    private static Task WriteError(HttpListenerContext ctx, int status, string message)
        => WriteJson(ctx, status, new { error = message });

    private static async Task WriteJsonRaw(HttpListenerContext ctx, int status, string json)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        var bytes = Encoding.UTF8.GetBytes(json);
        ctx.Response.ContentLength64 = bytes.LongLength;
        await ctx.Response.OutputStream.WriteAsync(bytes);
        ctx.Response.OutputStream.Close();
    }

    private static async Task WriteBytes(HttpListenerContext ctx, int status, string contentType, byte[] bytes)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = contentType;
        ctx.Response.ContentLength64 = bytes.LongLength;
        await ctx.Response.OutputStream.WriteAsync(bytes);
        ctx.Response.OutputStream.Close();
    }

    private Task<(byte[]? Png, string? Error)> CaptureScreenshotAsync(string? path)
        => Dispatcher.UIThread.InvokeAsync(() =>
        {
            var lifetime = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var window = lifetime?.MainWindow as HTTProof.Views.MainWindow
                ?? throw new InvalidOperationException("no window");

            if (!string.IsNullOrEmpty(path))
            {
                var scrollErr = ScrollResponseToPath(window, path);
                if (scrollErr is not null) return ((byte[]?)null, scrollErr);
            }

            var w = Math.Max(1, (int)Math.Ceiling(window.Bounds.Width));
            var h = Math.Max(1, (int)Math.Ceiling(window.Bounds.Height));
            var bitmap = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
            try
            {
                bitmap.Render(window);
                using var ms = new MemoryStream();
                bitmap.Save(ms, new PngBitmapEncoderOptions());
                return ((byte[]?)ms.ToArray(), (string?)null);
            }
            finally { bitmap.Dispose(); }
        }).GetTask();

    private string? ScrollResponseToPath(HTTProof.Views.MainWindow window, string path)
    {
        if (!_vm.HasResponse)
            return "cannot scroll to path: no response has been received yet";
        if (_vm.IsImageResponse)
            return "cannot scroll to path: response is an image, not text";
        var body = _vm.ResponseBody ?? "";
        if (body.Length == 0)
            return "cannot scroll to path: response body is empty";

        if (!JsonPathLocator.TryParse(path, out var segs, out var err))
            return "invalid path: " + err;
        if (!JsonPathLocator.TryLocate(body, segs, out var offset, out err))
            return err;

        var (line, _) = JsonPathLocator.OffsetToLineCol(body, offset);
        return window.TryCenterResponseAtLine(line);
    }

    private static async Task<T> UiAsync<T>(Func<T> fn)
        => await Dispatcher.UIThread.InvokeAsync(fn);

    private static async Task<T> UiAsync<T>(Func<Task<T>> fn)
        => await Dispatcher.UIThread.InvokeAsync(fn);

    private static string OpenApiSpec() => """
{
  "openapi": "3.0.3",
  "info": {
    "title": "HTTProof driving API",
    "version": "1.0.0",
    "description": "Local HTTP API for driving HTTProof from scripts and CI. All routes bind to 127.0.0.1 only."
  },
  "paths": {
    "/": {
      "get": {
        "summary": "This OpenAPI document",
        "responses": {
          "200": { "description": "OpenAPI 3 JSON", "content": { "application/json": {} } }
        }
      }
    },
    "/api/health": {
      "get": {
        "summary": "Liveness probe",
        "responses": {
          "200": { "description": "OK", "content": { "application/json": { "example": { "ok": true } } } }
        }
      }
    },
    "/api/state": {
      "get": {
        "summary": "Get the current request/response state",
        "responses": {
          "200": { "description": "State DTO", "content": { "application/json": {} } }
        }
      },
      "put": {
        "summary": "Replace-style patch: overwrite fields in the request",
        "requestBody": { "required": true, "content": { "application/json": { "schema": { "$ref": "#/components/schemas/RequestPatch" } } } },
        "responses": {
          "200": { "description": "New state", "content": { "application/json": {} } }
        }
      },
      "patch": {
        "summary": "Merge-style patch: same shape as PUT",
        "requestBody": { "required": true, "content": { "application/json": { "schema": { "$ref": "#/components/schemas/RequestPatch" } } } },
        "responses": {
          "200": { "description": "New state", "content": { "application/json": {} } }
        }
      }
    },
    "/api/send": {
      "post": {
        "summary": "Fire the current request (optionally patch first)",
        "requestBody": { "required": false, "content": { "application/json": { "schema": { "$ref": "#/components/schemas/RequestPatch" } } } },
        "responses": {
          "200": { "description": "Response received", "content": { "application/json": {} } },
          "502": { "description": "Transport error; body carries the state with an `error` field" }
        }
      }
    },
    "/api/screenshot": {
      "get": {
        "summary": "PNG screenshot of the main window",
        "parameters": [
          {
            "name": "path",
            "in": "query",
            "required": false,
            "schema": { "type": "string" },
            "description": "Optional path into the JSON response body. When supplied, the response pane is scrolled so that the referenced node is vertically centered in the visible area before the screenshot is captured. Three notations are accepted, disambiguated by the first character: JSONPath (leading '$', e.g. `$.data.items[0].id`), JSON Pointer per RFC 6901 (leading '/', e.g. `/data/items/0/id`), or dotted (default, e.g. `data.items[0].id`). If the path is malformed, does not resolve, or the current response is not scrollable JSON text, the request fails with HTTP 400 and no screenshot."
          }
        ],
        "responses": {
          "200": { "description": "PNG bytes", "content": { "image/png": {} } },
          "400": { "description": "Path invalid or does not resolve in the current response body", "content": { "application/json": {} } }
        }
      }
    },
    "/api/exit": {
      "post": {
        "summary": "Shut down the app",
        "responses": {
          "200": { "description": "Shutdown scheduled", "content": { "application/json": { "example": { "ok": true } } } }
        }
      }
    }
  },
  "components": {
    "schemas": {
      "Kv": {
        "type": "object",
        "properties": {
          "key": { "type": "string" },
          "value": { "type": "string" }
        }
      },
      "RequestPatch": {
        "type": "object",
        "properties": {
          "method": { "type": "string", "example": "GET" },
          "url":    { "type": "string", "example": "https://api.example.com/things" },
          "params": { "type": "array", "items": { "$ref": "#/components/schemas/Kv" } },
          "headers": { "type": "array", "items": { "$ref": "#/components/schemas/Kv" } },
          "auth": {
            "type": "object",
            "properties": {
              "kind":   { "type": "string", "enum": ["none", "basic", "bearer"] },
              "user":   { "type": "string" },
              "secret": { "type": "string" }
            }
          },
          "body": {
            "type": "object",
            "properties": {
              "kind": { "type": "string", "enum": ["none", "json", "text"] },
              "text": { "type": "string" }
            }
          }
        }
      }
    }
  }
}
""";

    public void Dispose()
    {
        try
        {
            _cts?.Cancel();
            _listener.Stop();
            _listener.Close();
        }
        catch { }
    }
}

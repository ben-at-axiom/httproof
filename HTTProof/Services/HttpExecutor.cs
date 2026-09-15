using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using HTTProof.Models;

namespace HTTProof.Services;

public sealed class HttpExecutor
{
    private static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
    })
    {
        Timeout = TimeSpan.FromMinutes(2),
    };

    public sealed record ExecuteRequest(
        string Method,
        string Url,
        IReadOnlyList<(string Key, string Value)> QueryParams,
        IReadOnlyList<(string Key, string Value)> Headers,
        AuthKind AuthKind,
        string AuthUser,
        string AuthSecret,
        BodyKind BodyKind,
        string Body
    );

    public sealed record ExecuteResult(ResponseSnapshot? Response, string? Error);

    public async Task<ExecuteResult> SendAsync(ExecuteRequest req, CancellationToken ct)
    {
        HttpRequestMessage? message = null;
        try
        {
            var url = BuildUrl(req.Url, req.QueryParams);
            message = new HttpRequestMessage(new HttpMethod(req.Method.ToUpperInvariant()), url);

            foreach (var (k, v) in req.Headers)
            {
                if (string.IsNullOrWhiteSpace(k)) continue;
                message.Headers.TryAddWithoutValidation(k, v);
            }

            switch (req.AuthKind)
            {
                case AuthKind.Basic:
                    var raw = $"{req.AuthUser}:{req.AuthSecret}";
                    var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
                    message.Headers.Authorization = new AuthenticationHeaderValue("Basic", b64);
                    break;
                case AuthKind.Bearer:
                    message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", req.AuthSecret);
                    break;
                case AuthKind.None:
                default:
                    break;
            }

            if (req.BodyKind != BodyKind.None && !string.IsNullOrEmpty(req.Body))
            {
                var mediaType = req.BodyKind == BodyKind.Json ? "application/json" : "text/plain";
                var content = new StringContent(req.Body, Encoding.UTF8);
                content.Headers.ContentType = new MediaTypeHeaderValue(mediaType) { CharSet = "utf-8" };

                foreach (var (k, v) in req.Headers)
                {
                    if (string.Equals(k, "Content-Type", StringComparison.OrdinalIgnoreCase))
                    {
                        content.Headers.ContentType = MediaTypeHeaderValue.Parse(v);
                        break;
                    }
                }

                message.Content = content;
            }

            var sw = Stopwatch.StartNew();
            using var response = await Client.SendAsync(message, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
            var bodyBytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            sw.Stop();

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var h in response.Headers)
                headers[h.Key] = string.Join(", ", h.Value);
            foreach (var h in response.Content.Headers)
                headers[h.Key] = string.Join(", ", h.Value);

            var contentType = response.Content.Headers.ContentType?.ToString();
            var text = IsImageContentType(contentType) ? "" : SafeDecode(bodyBytes);

            var snap = new ResponseSnapshot(
                Status: (int)response.StatusCode,
                StatusText: response.ReasonPhrase ?? "",
                Headers: headers,
                Body: text,
                BodyBytes: bodyBytes,
                ContentType: contentType,
                DurationMs: sw.ElapsedMilliseconds,
                ByteLength: bodyBytes.LongLength
            );

            return new ExecuteResult(snap, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new ExecuteResult(null, "canceled");
        }
        catch (Exception ex)
        {
            return new ExecuteResult(null, ex.Message);
        }
        finally
        {
            message?.Dispose();
        }
    }

    private static string BuildUrl(string urlRaw, IReadOnlyList<(string Key, string Value)> queryParams)
    {
        if (queryParams.Count == 0) return urlRaw;

        var sep = urlRaw.Contains('?') ? "&" : "?";
        var sb = new StringBuilder(urlRaw);
        var first = true;
        foreach (var (k, v) in queryParams)
        {
            if (string.IsNullOrWhiteSpace(k)) continue;
            sb.Append(first ? sep : "&");
            first = false;
            sb.Append(HttpUtility.UrlEncode(k));
            sb.Append('=');
            sb.Append(HttpUtility.UrlEncode(v));
        }
        return sb.ToString();
    }

    public static bool IsImageContentType(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType)) return false;
        return contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    private static string SafeDecode(byte[] bytes)
    {
        if (bytes.Length == 0) return "";
        try { return Encoding.UTF8.GetString(bytes); }
        catch { return $"<binary {bytes.LongLength} bytes>"; }
    }
}

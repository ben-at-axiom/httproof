using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HTTProof.Models;
using HTTProof.Services;

namespace HTTProof.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly HttpExecutor _executor = new();
    private CancellationTokenSource? _inflight;

    public MainViewModel()
    {
        MethodOptions = new ObservableCollection<string> { "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS" };
        Params = new ObservableCollection<KeyValueEntry>();
        Headers = new ObservableCollection<KeyValueEntry>();
        AuthKindOptions = new ObservableCollection<AuthKind> { AuthKind.None, AuthKind.Basic, AuthKind.Bearer };
        BodyKindOptions = new ObservableCollection<BodyKind> { BodyKind.None, BodyKind.Json, BodyKind.Text };
    }

    public ObservableCollection<string> MethodOptions { get; }
    public ObservableCollection<AuthKind> AuthKindOptions { get; }
    public ObservableCollection<BodyKind> BodyKindOptions { get; }

    [ObservableProperty]
    public partial string Method { get; set; } = "GET";

    [ObservableProperty]
    public partial string Url { get; set; } = "";

    public ObservableCollection<KeyValueEntry> Params { get; }
    public ObservableCollection<KeyValueEntry> Headers { get; }

    [ObservableProperty]
    public partial AuthKind Auth { get; set; } = AuthKind.None;

    [ObservableProperty]
    public partial string AuthUser { get; set; } = "";

    [ObservableProperty]
    public partial string AuthSecret { get; set; } = "";

    [ObservableProperty]
    public partial BodyKind Body { get; set; } = BodyKind.None;

    [ObservableProperty]
    public partial string BodyText { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial int? StatusCode { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    [ObservableProperty]
    public partial string StatusBadgeClass { get; set; } = "neutral";

    [ObservableProperty]
    public partial long? DurationMs { get; set; }

    [ObservableProperty]
    public partial long? ByteLength { get; set; }

    [ObservableProperty]
    public partial string ResponseBody { get; set; } = "";

    [ObservableProperty]
    public partial string ResponseContentType { get; set; } = "";

    [ObservableProperty]
    public partial IReadOnlyDictionary<string, string> ResponseHeaders { get; set; } = new Dictionary<string, string>();

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool HasResponse { get; set; }

    [ObservableProperty]
    public partial Bitmap? ResponseImage { get; set; }

    [ObservableProperty]
    public partial string? ResponseImageError { get; set; }

    public bool IsImageResponse => HasResponse && ResponseImage is not null;
    public bool IsTextResponse => HasResponse && ResponseImage is null && ResponseImageError is null;
    public bool ShowIdlePlaceholder => !HasResponse && ErrorMessage is null;

    partial void OnHasResponseChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowIdlePlaceholder));
        OnPropertyChanged(nameof(IsImageResponse));
        OnPropertyChanged(nameof(IsTextResponse));
    }
    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(ShowIdlePlaceholder));
    partial void OnResponseImageChanged(Bitmap? value)
    {
        OnPropertyChanged(nameof(IsImageResponse));
        OnPropertyChanged(nameof(IsTextResponse));
    }
    partial void OnResponseImageErrorChanged(string? value) => OnPropertyChanged(nameof(IsTextResponse));

    [RelayCommand(CanExecute = nameof(CanSend))]
    public Task SendAsync() => ExecuteAsync();

    public bool CanSend() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    public void Cancel()
    {
        _inflight?.Cancel();
    }

    public bool CanCancel() => IsBusy;

    public async Task<HttpExecutor.ExecuteResult> ExecuteAsync()
    {
        _inflight?.Cancel();
        _inflight = new CancellationTokenSource();
        var ct = _inflight.Token;

        IsBusy = true;
        ErrorMessage = null;

        if (Body == BodyKind.Json && JsonPretty.TryFormat(BodyText, out var prettyBody))
            BodyText = prettyBody;

        var req = new HttpExecutor.ExecuteRequest(
            Method: Method,
            Url: Url,
            QueryParams: SnapshotEnabled(Params),
            Headers: SnapshotEnabled(Headers),
            AuthKind: Auth,
            AuthUser: AuthUser,
            AuthSecret: AuthSecret,
            BodyKind: Body,
            Body: BodyText
        );

        var result = await _executor.SendAsync(req, ct).ConfigureAwait(true);

        ResponseImage?.Dispose();
        ResponseImage = null;
        ResponseImageError = null;

        if (result.Response is { } snap)
        {
            StatusCode = snap.Status;
            StatusText = snap.StatusText;
            StatusBadgeClass = ClassifyStatus(snap.Status);
            DurationMs = snap.DurationMs;
            ByteLength = snap.ByteLength;
            ResponseContentType = snap.ContentType ?? "";
            ResponseHeaders = snap.Headers;

            if (HttpExecutor.IsImageContentType(snap.ContentType) && snap.BodyBytes.Length > 0)
            {
                try
                {
                    using var ms = new MemoryStream(snap.BodyBytes, writable: false);
                    ResponseImage = new Bitmap(ms);
                    ResponseBody = "";
                }
                catch (Exception ex)
                {
                    ResponseImageError = ex.Message;
                    ResponseBody = $"<image decode failed: {ex.Message}>";
                }
            }
            else if (LooksJson(snap.ContentType, snap.Body) && JsonPretty.TryFormat(snap.Body, out var pretty))
                ResponseBody = pretty;
            else
                ResponseBody = snap.Body;

            HasResponse = true;
        }
        else
        {
            StatusCode = null;
            StatusText = "";
            StatusBadgeClass = "neutral";
            DurationMs = null;
            ByteLength = null;
            ResponseContentType = "";
            ResponseHeaders = new Dictionary<string, string>();
            ResponseBody = "";
            ErrorMessage = result.Error;
            HasResponse = false;
        }

        IsBusy = false;
        return result;
    }

    private static List<(string, string)> SnapshotEnabled(ObservableCollection<KeyValueEntry> src)
    {
        var list = new List<(string, string)>(src.Count);
        foreach (var e in src)
        {
            if (!e.Enabled) continue;
            if (string.IsNullOrWhiteSpace(e.Key)) continue;
            list.Add((e.Key, e.Value ?? ""));
        }
        return list;
    }

    private static string ClassifyStatus(int status) => status switch
    {
        >= 200 and < 300 => "ok",
        >= 300 and < 400 => "redir",
        >= 400 and < 500 => "client-err",
        >= 500 => "server-err",
        _ => "neutral",
    };

    private static bool LooksJson(string? contentType, string body)
    {
        if (!string.IsNullOrEmpty(contentType) && contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
            return true;
        var trimmed = body.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }
}

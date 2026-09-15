using System.Collections.Generic;
using System.Text.Json.Serialization;
using HTTProof.Models;

namespace HTTProof.Services;

public sealed class StateDto
{
    [JsonPropertyName("request")] public RequestDto? Request { get; set; }
    [JsonPropertyName("response")] public ResponseDto? Response { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
    [JsonPropertyName("isBusy")] public bool IsBusy { get; set; }
    [JsonPropertyName("hasResponse")] public bool HasResponse { get; set; }
}

public sealed class RequestDto
{
    [JsonPropertyName("method")]  public string? Method { get; set; }
    [JsonPropertyName("url")]     public string? Url { get; set; }
    [JsonPropertyName("params")]  public List<KvDto>? Params { get; set; }
    [JsonPropertyName("headers")] public List<KvDto>? Headers { get; set; }
    [JsonPropertyName("auth")]    public AuthDto? Auth { get; set; }
    [JsonPropertyName("body")]    public BodyDto? Body { get; set; }
}

public sealed class KvDto
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
    [JsonPropertyName("key")]     public string Key { get; set; } = "";
    [JsonPropertyName("value")]   public string Value { get; set; } = "";
}

public sealed class AuthDto
{
    [JsonPropertyName("kind")]   public AuthKind Kind { get; set; }
    [JsonPropertyName("user")]   public string? User { get; set; }
    [JsonPropertyName("secret")] public string? Secret { get; set; }
}

public sealed class BodyDto
{
    [JsonPropertyName("kind")] public BodyKind Kind { get; set; }
    [JsonPropertyName("text")] public string? Text { get; set; }
}

public sealed class ResponseDto
{
    [JsonPropertyName("status")]      public int Status { get; set; }
    [JsonPropertyName("statusText")]  public string StatusText { get; set; } = "";
    [JsonPropertyName("headers")]     public Dictionary<string, string> Headers { get; set; } = new();
    [JsonPropertyName("body")]        public string Body { get; set; } = "";
    [JsonPropertyName("contentType")] public string? ContentType { get; set; }
    [JsonPropertyName("durationMs")]  public long DurationMs { get; set; }
    [JsonPropertyName("byteLength")]  public long ByteLength { get; set; }
}

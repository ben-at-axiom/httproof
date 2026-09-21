using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace HTTProof.Services;

public static class JsonPathLocator
{
    public sealed record PathSegment
    {
        public string? Key { get; init; }
        public int? Index { get; init; }

        public override string ToString() =>
            Key is not null && Index is not null ? $"'{Key}'/[{Index}]" :
            Key is not null ? $"'{Key}'" :
            Index is not null ? $"[{Index}]" : "(empty)";
    }

    public static bool TryParse(string input, out List<PathSegment> segments, out string error)
    {
        segments = new List<PathSegment>();
        error = "";
        if (string.IsNullOrEmpty(input)) { error = "path is empty"; return false; }

        if (input[0] == '$') return TryParseJsonPath(input, segments, out error);
        if (input[0] == '/') return TryParseJsonPointer(input, segments, out error);
        return TryParseDotted(input, segments, out error);
    }

    public static bool TryLocate(string json, IReadOnlyList<PathSegment> segments, out int charOffset, out string error)
    {
        charOffset = 0;
        error = "";
        if (string.IsNullOrEmpty(json)) { error = "response body is empty"; return false; }

        var bytes = Encoding.UTF8.GetBytes(json);
        try
        {
            var reader = new Utf8JsonReader(bytes, isFinalBlock: true, state: default);
            if (!reader.Read()) { error = "response body has no JSON value"; return false; }
            if (!Descend(ref reader, segments, 0, out long byteOffset, out error)) return false;
            charOffset = Encoding.UTF8.GetCharCount(bytes, 0, (int)byteOffset);
            return true;
        }
        catch (JsonException ex)
        {
            error = "response body is not valid JSON: " + ex.Message;
            return false;
        }
    }

    public static (int Line, int Col) OffsetToLineCol(string text, int offset)
    {
        int line = 1, col = 1;
        int end = Math.Min(offset, text.Length);
        for (int i = 0; i < end; i++)
        {
            if (text[i] == '\n') { line++; col = 1; }
            else if (text[i] != '\r') col++;
        }
        return (line, col);
    }

    private static bool Descend(ref Utf8JsonReader r, IReadOnlyList<PathSegment> segs, int i, out long byteOffset, out string error)
    {
        if (i >= segs.Count)
        {
            byteOffset = r.TokenStartIndex;
            error = "";
            return true;
        }

        var seg = segs[i];
        switch (r.TokenType)
        {
            case JsonTokenType.StartObject:
                if (seg.Key is null)
                {
                    byteOffset = 0;
                    error = $"segment {i + 1}: cannot use array index {seg.Index} to index into an object";
                    return false;
                }
                while (r.Read())
                {
                    if (r.TokenType == JsonTokenType.EndObject) break;
                    // TokenType is PropertyName here.
                    var name = r.GetString();
                    if (name == seg.Key)
                    {
                        r.Read();
                        return Descend(ref r, segs, i + 1, out byteOffset, out error);
                    }
                    r.Skip();
                }
                byteOffset = 0;
                error = $"segment {i + 1}: object has no key '{seg.Key}'";
                return false;

            case JsonTokenType.StartArray:
                if (seg.Index is null)
                {
                    byteOffset = 0;
                    error = $"segment {i + 1}: cannot use key '{seg.Key}' to index into an array";
                    return false;
                }
                int want = seg.Index.Value;
                int cur = 0;
                while (r.Read())
                {
                    if (r.TokenType == JsonTokenType.EndArray) break;
                    if (cur == want) return Descend(ref r, segs, i + 1, out byteOffset, out error);
                    r.Skip();
                    cur++;
                }
                byteOffset = 0;
                error = $"segment {i + 1}: array has no index {want} (length {cur})";
                return false;

            default:
                byteOffset = 0;
                error = $"segment {i + 1}: cannot traverse into a scalar value";
                return false;
        }
    }

    private static PathSegment MakeNamedSegment(string name)
    {
        if (name.Length > 0 && int.TryParse(name, out int n) && n >= 0 && !name.StartsWith('+'))
            return new PathSegment { Key = name, Index = n };
        return new PathSegment { Key = name };
    }

    private static bool TryParseJsonPath(string input, List<PathSegment> segs, out string error)
    {
        error = "";
        int i = 1;
        while (i < input.Length)
        {
            char c = input[i];
            if (c == '.')
            {
                i++;
                if (i >= input.Length) { error = "trailing '.' in path"; return false; }
                if (input[i] == '.') { error = "recursive descent '..' not supported"; return false; }
                int start = i;
                while (i < input.Length && input[i] != '.' && input[i] != '[') i++;
                if (i == start) { error = "empty segment after '.'"; return false; }
                var name = input[start..i];
                if (name == "*") { error = "wildcard '*' not supported"; return false; }
                segs.Add(MakeNamedSegment(name));
            }
            else if (c == '[')
            {
                int close = input.IndexOf(']', i);
                if (close < 0) { error = "unclosed '[' in path"; return false; }
                var inner = input[(i + 1)..close].Trim();
                if (inner.Length == 0) { error = "empty '[]' in path"; return false; }
                if (inner.Length >= 2 && (inner[0] == '\'' || inner[0] == '"') && inner[^1] == inner[0])
                {
                    var name = inner[1..^1];
                    segs.Add(new PathSegment { Key = name });
                }
                else if (int.TryParse(inner, out int idx) && idx >= 0)
                {
                    segs.Add(new PathSegment { Index = idx });
                }
                else
                {
                    error = $"invalid bracket segment '[{inner}]'";
                    return false;
                }
                i = close + 1;
            }
            else
            {
                error = $"unexpected character '{c}' at position {i} (JSONPath expects '.' or '[')";
                return false;
            }
        }
        return true;
    }

    private static bool TryParseJsonPointer(string input, List<PathSegment> segs, out string error)
    {
        error = "";
        var parts = input.Split('/');
        // parts[0] is "" because input starts with '/'.
        for (int k = 1; k < parts.Length; k++)
        {
            // RFC 6901: decode ~1 -> '/' first, then ~0 -> '~'.
            var token = parts[k].Replace("~1", "/").Replace("~0", "~");
            segs.Add(MakeNamedSegment(token));
        }
        return true;
    }

    private static bool TryParseDotted(string input, List<PathSegment> segs, out string error)
    {
        error = "";
        int i = 0;
        while (i < input.Length)
        {
            char c = input[i];
            if (c == '.') { error = $"unexpected '.' at position {i}"; return false; }

            if (c == '[')
            {
                if (segs.Count == 0)
                {
                    error = "path cannot start with '['";
                    return false;
                }
            }
            else
            {
                int start = i;
                while (i < input.Length && input[i] != '.' && input[i] != '[') i++;
                var name = input[start..i];
                if (name.Length == 0) { error = "empty segment"; return false; }
                segs.Add(MakeNamedSegment(name));
            }

            while (i < input.Length && input[i] == '[')
            {
                int close = input.IndexOf(']', i);
                if (close < 0) { error = "unclosed '[' in path"; return false; }
                var inner = input[(i + 1)..close].Trim();
                if (!int.TryParse(inner, out int idx) || idx < 0)
                {
                    error = $"invalid array index '[{inner}]' (dotted form only allows non-negative integers in brackets)";
                    return false;
                }
                segs.Add(new PathSegment { Index = idx });
                i = close + 1;
            }

            if (i < input.Length)
            {
                if (input[i] == '.') { i++; if (i >= input.Length) { error = "trailing '.' in path"; return false; } }
                else if (input[i] != '[') { error = $"unexpected character '{input[i]}' at position {i}"; return false; }
            }
        }
        if (segs.Count == 0) { error = "path has no segments"; return false; }
        return true;
    }
}

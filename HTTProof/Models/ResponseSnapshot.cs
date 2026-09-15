using System.Collections.Generic;

namespace HTTProof.Models;

public sealed record ResponseSnapshot(
    int Status,
    string StatusText,
    IReadOnlyDictionary<string, string> Headers,
    string Body,
    byte[] BodyBytes,
    string? ContentType,
    long DurationMs,
    long ByteLength
);

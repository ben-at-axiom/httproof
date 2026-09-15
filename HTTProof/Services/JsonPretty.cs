using System;
using System.Text.Json;

namespace HTTProof.Services;

public static class JsonPretty
{
    private static readonly JsonWriterOptions PrettyOpts = new()
    {
        Indented = true,
        SkipValidation = false,
    };

    public static bool TryFormat(string input, out string formatted)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            formatted = input;
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(input);
            using var stream = new System.IO.MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, PrettyOpts))
            {
                doc.WriteTo(writer);
            }
            formatted = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            return true;
        }
        catch (JsonException)
        {
            formatted = input;
            return false;
        }
        catch (ArgumentException)
        {
            formatted = input;
            return false;
        }
    }
}

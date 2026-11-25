using System.Text.Json;

namespace NexaAI.Core.Application.Common;

/// <summary>Small helpers for reading loosely-shaped JSON returned by the AI service.</summary>
public static class JsonHelpers
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Creates a JSON document from an object using camel-case property names.</summary>
    public static JsonDocument ToDocument(object? value) =>
        JsonDocument.Parse(JsonSerializer.Serialize(value ?? new { }, SerializerOptions));

    /// <summary>Creates an empty JSON array document.</summary>
    public static JsonDocument EmptyArray() => JsonDocument.Parse("[]");

    /// <summary>Creates an empty JSON object document.</summary>
    public static JsonDocument EmptyObject() => JsonDocument.Parse("{}");

    /// <summary>Finds a property by name, ignoring case and underscores.</summary>
    public static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(Normalize(property.Name), Normalize(name), StringComparison.Ordinal))
            {
                value = property.Value;
                return true;
            }
        }

        return false;
    }

    /// <summary>Reads a string property using several accepted aliases.</summary>
    public static string? GetString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProperty(element, name, out var value))
            {
                if (value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString();
                }

                if (value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                {
                    return value.ToString();
                }
            }
        }

        return null;
    }

    /// <summary>Reads an integer property using several accepted aliases.</summary>
    public static int? GetInt(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProperty(element, name, out var value) &&
                value.ValueKind == JsonValueKind.Number &&
                value.TryGetInt32(out var result))
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>Reads a floating point property using several accepted aliases.</summary>
    public static double? GetDouble(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProperty(element, name, out var value) &&
                value.ValueKind == JsonValueKind.Number &&
                value.TryGetDouble(out var result))
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>Reads a boolean property using several accepted aliases.</summary>
    public static bool? GetBool(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(element, name, out var value))
            {
                continue;
            }

            if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                return value.GetBoolean();
            }

            if (value.ValueKind == JsonValueKind.String &&
                bool.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    /// <summary>Returns an array child property, or an empty array when absent.</summary>
    public static JsonElement GetArray(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.Array)
            {
                return value;
            }
        }

        return JsonDocument.Parse("[]").RootElement.Clone();
    }

    /// <summary>
    /// Extracts a float vector from common embedding response shapes: a bare array,
    /// {"embedding": [...]}, {"vector": [...]} or OpenAI-style {"data":[{"embedding": [...]}]}.
    /// </summary>
    public static float[]? TryReadVector(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return TryReadNumberArray(element);
        }

        if (TryGetProperty(element, "embedding", out var embedding) ||
            TryGetProperty(element, "vector", out embedding) ||
            TryGetProperty(element, "values", out embedding))
        {
            var vector = TryReadNumberArray(embedding);
            if (vector is { Length: > 0 })
            {
                return vector;
            }
        }

        if (TryGetProperty(element, "data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                var vector = TryReadVector(item);
                if (vector is { Length: > 0 })
                {
                    return vector;
                }
            }
        }

        return null;
    }

    private static float[]? TryReadNumberArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var values = new List<float>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number || !item.TryGetSingle(out var value))
            {
                return null;
            }

            values.Add(value);
        }

        return values.ToArray();
    }

    private static string Normalize(string value) => value.Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
}

using System.Globalization;
using System.Text.Json;

namespace Qx.Mcp;

public static class McpArguments
{
    public static string Str(JsonElement args, string key) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(key, out JsonElement v) ? v.GetString() ?? "" : "";

    public static string Str(JsonElement args, string key, string fallback) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(key, out JsonElement value)
            ? value.GetString() ?? fallback
            : fallback;

    public static int Int(JsonElement args, string key) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(key, out JsonElement v) && v.TryGetInt32(out int i) ? i : 0;

    public static int Int(JsonElement args, string key, int fallback) =>
        args.ValueKind == JsonValueKind.Object &&
        args.TryGetProperty(key, out JsonElement value) &&
        value.TryGetInt32(out int result)
            ? result
            : fallback;

    public static int BoundedInt(
        JsonElement args,
        string key,
        int fallback,
        int minimum,
        int maximum)
    {
        if (args.ValueKind != JsonValueKind.Object ||
            !args.TryGetProperty(key, out JsonElement value))
        {
            return fallback;
        }
        if (!value.TryGetInt32(out int result))
            throw new ArgumentException($"'{key}' must be an integer.", key);
        if (result < minimum || result > maximum)
            throw new ArgumentOutOfRangeException(key, result, $"'{key}' must be between {minimum} and {maximum}.");
        return result;
    }

    public static bool Bool(JsonElement args, string key, bool fallback = false) =>
        args.ValueKind != JsonValueKind.Object ||
        !args.TryGetProperty(key, out JsonElement value)
            ? fallback
            : value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => throw new ArgumentException($"'{key}' must be a boolean.", key)
            };

    public static long Long(JsonElement args, string key)
    {
        if (args.ValueKind != JsonValueKind.Object ||
            !args.TryGetProperty(key, out JsonElement value))
        {
            throw new ArgumentException($"Missing identifier '{key}'.", key);
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long numeric))
            return numeric;
        if (value.ValueKind == JsonValueKind.String &&
            long.TryParse(
                value.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long text))
        {
            return text;
        }

        throw new ArgumentException(
            $"Identifier '{key}' must be a signed 64-bit decimal integer or string.",
            key);
    }

    public static long Long(JsonElement args, string key, long fallback) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(key, out _)
            ? Long(args, key)
            : fallback;

    public static long? OptionalPositiveLong(JsonElement args, string key)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(key, out _))
            return null;
        long value = Long(args, key);
        if (value <= 0)
            throw new ArgumentOutOfRangeException(key, value, $"'{key}' must be greater than zero.");
        return value;
    }

    public static object[] Values(JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty("values", out JsonElement arr) || arr.ValueKind != JsonValueKind.Array)
            return [];

        var list = new List<object>();
        foreach (JsonElement element in arr.EnumerateArray())
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Number:
                    if (element.TryGetInt32(out int i)) list.Add(i);
                    else list.Add(element.GetInt64());
                    break;
                case JsonValueKind.String:
                    list.Add(element.GetString()!);
                    break;
                case JsonValueKind.True:
                    list.Add(true);
                    break;
                case JsonValueKind.False:
                    list.Add(false);
                    break;
            }
        }
        return list.ToArray();
    }

}

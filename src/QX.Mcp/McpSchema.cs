namespace Qx.Mcp;

public static class McpSchema
{
    private const int MaxOffset = 1000000;
    public static object Schema(params (string Name, string Type, string Desc)[] props) =>
        MixedSchema(props);

    public static object OptionalSchema(
        params (string Name, string Type, string Desc, object? Default, int? Minimum, int? Maximum)[] props) =>
        MixedSchema([], props);

    public static object MixedSchema(
        (string Name, string Type, string Desc)[] required,
        params (string Name, string Type, string Desc, object? Default, int? Minimum, int? Maximum)[] optional)
    {
        var properties = new Dictionary<string, object?>();
        foreach ((string name, string type, string desc) in required)
            properties[name] = RequiredProperty(type, desc);

        foreach ((string name, string type, string desc, object? default_value, int? minimum, int? maximum) in optional)
        {
            Dictionary<string, object?> property = RequiredProperty(type, desc);
            if (default_value is not null)
                property["default"] = default_value;
            if (minimum.HasValue)
                property["minimum"] = minimum.Value;
            if (maximum.HasValue)
                property["maximum"] = maximum.Value;
            properties[name] = property;
        }

        return new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required.Select(x => x.Name).ToArray(),
            ["additionalProperties"] = false
        };
    }

    public static Dictionary<string, object?> RequiredProperty(string type, string description) =>
        type == "id"
            ? new Dictionary<string, object?>
            {
                ["oneOf"] = new object[]
                {
                    new Dictionary<string, object?> { ["type"] = "integer" },
                    new Dictionary<string, object?>
                    {
                        ["type"] = "string",
                        ["pattern"] = "^-?[0-9]+$"
                    }
                },
                ["description"] = description
            }
            : new Dictionary<string, object?> { ["type"] = type, ["description"] = description };

    public static (string Name, string Type, string Desc, object? Default, int? Minimum, int? Maximum)
        OffsetProperty(string subject) =>
        ("offset", "integer", $"zero-based index of the first returned {subject}", 0, 0, MaxOffset);

}

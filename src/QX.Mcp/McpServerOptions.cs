namespace Qx.Mcp;

public sealed record McpServerOptions
{
    public required string Name { get; init; }
    public string? Title { get; init; }
    public string? Version { get; init; }
    public required int Port { get; init; }
    public string? ConfigurationPath { get; init; }
    public Func<string>? Instructions { get; init; }
    public Func<McpRuntimeCapability>? RuntimeCapabilities { get; init; }

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        if (Port is < 1 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(Port));
        if (ConfigurationPath is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(ConfigurationPath);
            _ = Path.GetFullPath(ConfigurationPath);
        }
    }
}

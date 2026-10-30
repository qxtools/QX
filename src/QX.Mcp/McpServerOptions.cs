namespace Qx.Mcp;

public sealed record McpServerOptions
{
    public required string Name { get; init; }
    public string? Title { get; init; }
    public string? Version { get; init; }
    public required int Port { get; init; }
    public bool SearchPorts { get; init; } = true;
    public int PortSearchCount { get; init; } = 32;
    public string? ConfigurationPath { get; init; }
    public Func<string>? Instructions { get; init; }
    public Func<McpRuntimeCapability>? RuntimeCapabilities { get; init; }

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        if (Port is < 1 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(Port));
        if (PortSearchCount < 1)
            throw new ArgumentOutOfRangeException(nameof(PortSearchCount));
        if (ConfigurationPath is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(ConfigurationPath);
            _ = Path.GetFullPath(ConfigurationPath);
        }
    }
}

using Qx.ClientCatalog.InstalledClients;
using Qx.Interception.GEarth;

namespace Qx.Hosting;

public record GameHostOptions
{
    public GEarthOptions GEarth { get; init; } = new();

    public string GameDataCachePath { get; init; } = Path.Combine(StoragePaths.Cache, "gamedata");

    public string HeaderCatalogCachePath { get; init; } = Path.Combine(
        StoragePaths.Cache,
        "header-catalogs");

    public InstalledClientMonitorOptions InstalledClients { get; init; } = new();

    public TimeSpan HttpTimeout { get; init; } = TimeSpan.FromMinutes(10);

    public TimeSpan TransportRetryDelay { get; init; } = TimeSpan.FromSeconds(1);

    public bool ReconnectTransport { get; init; }

    public bool EnableTransport { get; init; } = true;

    public bool EnableFallbackCatalogs { get; init; } = true;

    public bool EnableClientMonitoring { get; init; } = true;

    internal void Validate()
    {
        ArgumentNullException.ThrowIfNull(GEarth);
        ArgumentException.ThrowIfNullOrWhiteSpace(GameDataCachePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(HeaderCatalogCachePath);
        ArgumentNullException.ThrowIfNull(InstalledClients);
        if (HttpTimeout != Timeout.InfiniteTimeSpan && HttpTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(HttpTimeout));
        if (TransportRetryDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(TransportRetryDelay));
        _ = Path.GetFullPath(GameDataCachePath);
        _ = Path.GetFullPath(HeaderCatalogCachePath);
    }
}

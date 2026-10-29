namespace Qx.Hosting;

public enum HostServicePhase
{
    Disabled,
    Pending,
    Running,
    Completed,
    Canceled,
    Faulted
}

public sealed record HostServiceStatus(
    bool Enabled,
    HostServicePhase Phase,
    Exception? Error = null);

public sealed record GameHostStatus(
    bool Started,
    bool Disposed,
    HostServiceStatus Transport,
    HostServiceStatus FallbackCatalogs,
    HostServiceStatus HeaderPreparation);

public sealed record HeaderCatalogFailure(
    string Path,
    Exception Error);

using Qx.ClientCatalog.InstalledClients;
using Qx.ClientCatalog;
using Qx.Diagnostics;
using Qx.Game.Api;
using Qx.Game.Data;
using Qx.Game.Messages;
using Qx.Game.Snapshots;
using Qx.Game;
using Qx.Interception.GEarth;
using Qx.Protocol;

namespace Qx.Hosting;

public sealed class GameHost : IDisposable, IAsyncDisposable
{
    readonly GameHostOptions _options;
    readonly CancellationTokenSource _lifetime = new();
    readonly object _gate = new();
    readonly Dictionary<string, Exception> _header_catalog_errors = [];
    readonly GameApi application_runtime;
    readonly bool _owns_http;
    Task _transport_task = Task.CompletedTask;
    Task _fallback_task = Task.CompletedTask;
    Task _header_task = Task.CompletedTask;
    Task? _startup;
    Task? _disposal;
    ClientCatalogLoadResult? _fallback_catalog;
    bool _transport_started;
    bool _fallback_started;
    bool _header_started;
    bool _session_established;
    bool _started;
    bool _disposed;

    public GameHost(GameHostOptions options, HttpClient? http = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        _owns_http = http is null;
        Messages = MessageManager.CreateWithEmbeddedMap();
        Contracts = new MessageContractCatalog(Messages.Registry, MessageContracts.All);
        Http = http ?? new HttpClient { Timeout = _options.HttpTimeout };
        try
        {
            if (_owns_http)
                Http.DefaultRequestHeaders.UserAgent.ParseAdd("QX");
            Extension = new GEarthExtension(_options.GEarth, Messages);
            Extension.Connected += _ => Volatile.Write(ref _session_established, true);
            Game = new GameState(new GameData(Http, _options.GameDataCachePath));
            Game.Attach(Extension);
            application_runtime = new GameApi(Extension, Game, Contracts);
            application_runtime.ObserverFailed += error => Diag.Error(error.ToString(), "application");
            Api = application_runtime;
            Queries = new GameQueries(
                Game,
                Api,
                () => Extension.Session,
                () => Extension.IsInterceptorConnected,
                () => Extension.Session is not null && Messages.HasCatalog(),
                () => Extension.Session is not null && Messages.GetWireProfile().IsAnalyzed,
                () => Extension.Session is not null && Messages.GetWireProfile().HasExactIncomingLayout(),
                () => Extension.Session is not null
                    ? Messages.GetWireProfile().MissingIncomingCapabilities()
                    : []);
            if (_options.EnableClientMonitoring)
            {
                InstalledClients = new InstalledClientMonitor(Http, _options.InstalledClients);
                HeaderCatalogs = new HeaderCatalogCoordinator(
                    InstalledClients,
                    Path.GetFullPath(_options.HeaderCatalogCachePath));
                HeaderCatalogs.PreparationChanged += HeaderPreparationChanged;
                Extension.SessionCatalogSelector = new PreparedSessionCatalogSelector(
                    HeaderCatalogs,
                    Messages.Registry);
                Extension.CatalogReadiness = HeaderCatalogs;
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public MessageManager Messages { get; }

    public MessageContractCatalog Contracts { get; }

    public HttpClient Http { get; }

    public GEarthExtension Extension { get; }

    public GameState Game { get; }

    public IGameApi Api { get; }

    public GameQueries Queries { get; }

    public InstalledClientMonitor? InstalledClients { get; }

    public HeaderCatalogCoordinator? HeaderCatalogs { get; }

    public Task TransportTask
    {
        get
        {
            lock (_gate)
                return _transport_task;
        }
    }

    public Task FallbackCatalogTask
    {
        get
        {
            lock (_gate)
                return _fallback_task;
        }
    }

    public Task HeaderPreparationTask
    {
        get
        {
            lock (_gate)
                return _header_task;
        }
    }

    public ClientCatalogLoadResult? FallbackCatalog =>
        Volatile.Read(ref _fallback_catalog);

    public IReadOnlyList<HeaderCatalogFailure> HeaderCatalogErrors
    {
        get
        {
            lock (_gate)
            {
                return _header_catalog_errors
                    .OrderBy(value => value.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(value => new HeaderCatalogFailure(value.Key, value.Value))
                    .ToArray();
            }
        }
    }

    public GameHostStatus Status
    {
        get
        {
            lock (_gate)
            {
                return new GameHostStatus(
                    _started,
                    _disposed,
                    Service(_options.EnableTransport, _transport_started, _transport_task),
                    Service(_options.EnableFallbackCatalogs, _fallback_started, _fallback_task),
                    HeaderStatus());
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Task startup;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_startup is null)
            {
                _started = true;
                _startup = StartCoreAsync();
            }
            startup = _startup;
        }
        return startup.WaitAsync(cancellationToken);
    }

    Task StartCoreAsync()
    {
        if (_options.EnableFallbackCatalogs)
        {
            lock (_gate)
                _fallback_started = true;
        }
        if (_options.EnableFallbackCatalogs && HeaderCatalogs is null)
        {
            Task fallback = LoadFallbackCatalogAsync();
            lock (_gate)
                _fallback_task = fallback;
        }
        if (HeaderCatalogs is not null)
        {
            Task headers = PrepareHeadersAsync(HeaderCatalogs);
            lock (_gate)
            {
                _header_task = headers;
                _header_started = true;
            }
        }

        if (_options.EnableTransport)
        {
            lock (_gate)
            {
                if (_disposed)
                    return Task.CompletedTask;
                Task transport = Task.Run(RunTransportAsync);
                _transport_task = transport;
                _transport_started = true;
            }
        }
        return Task.CompletedTask;
    }

    async Task PrepareHeadersAsync(HeaderCatalogCoordinator catalogs)
    {
        await catalogs.StartAsync(_lifetime.Token).ConfigureAwait(false);
        await catalogs.WaitForIdleAsync(_lifetime.Token).ConfigureAwait(false);
    }

    async Task RunTransportAsync()
    {
        while (!_lifetime.IsCancellationRequested)
        {
            try
            {
                await Extension.RunAsync(_lifetime.Token).ConfigureAwait(false);
                if (_lifetime.IsCancellationRequested ||
                    (!_options.ReconnectTransport && Volatile.Read(ref _session_established)))
                    return;
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
                return;
            }
            catch when (_options.ReconnectTransport || !Volatile.Read(ref _session_established))
            {
            }
            await Task.Delay(_options.TransportRetryDelay, _lifetime.Token).ConfigureAwait(false);
        }
    }

    async Task LoadFallbackCatalogAsync()
    {
        var resolver = new ClientCatalogResolver(
            Http,
            _options.InstalledClients.LauncherDataPath,
            _options.InstalledClients.CacheRootPath);
        ClientCatalogLoadResult catalog = await ClientCatalogBootstrapper.LoadInstalledAsync(
            Messages,
            resolver,
            _ => Extension.RebindInterceptors(),
            cancellationToken: _lifetime.Token).ConfigureAwait(false);
        Volatile.Write(ref _fallback_catalog, catalog);
        if (catalog.Resolution is { } loaded)
            Diag.Info($"Loaded {loaded.Version} header fallback from {loaded.Source}", "protocol");
        else if (catalog.Error is not null)
            Diag.Warn($"Header fallback unavailable: {catalog.Error.Message}", "protocol");
    }

    void HeaderPreparationChanged(object? sender, HeaderCatalogPreparationChangedEventArgs args)
    {
        HeaderCatalogPreparationStatus status = args.Status;
        if (status.Stage == HeaderCatalogPreparationStage.Failed)
        {
            Exception error = status.Error ?? new InvalidDataException(
                $"{status.Candidate.Version} header preparation failed.");
            lock (_gate)
                _header_catalog_errors[status.NormalizedPath] = error;
            Diag.Warn(
                $"{status.Candidate.Version} header preparation failed: {error.Message}",
                "protocol");
            return;
        }
        if (status.Stage == HeaderCatalogPreparationStage.Ready)
        {
            lock (_gate)
                _header_catalog_errors.Remove(status.NormalizedPath);
        }
        if (status.Stage != HeaderCatalogPreparationStage.Ready)
            return;
        if (!HeaderCatalogs!.TryGetByPath(status.NormalizedPath, out PreparedHeaderCatalog? prepared) ||
            prepared is null)
            return;
        IntegratePreparedCatalog(prepared);
    }

    internal bool IntegratePreparedCatalog(PreparedHeaderCatalog prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        try
        {
            MessageCatalog catalog = ClientCatalogFactory.Create(prepared);
            lock (_gate)
                _header_catalog_errors.Remove(prepared.NormalizedPath);
            string build = prepared.Catalog.ClientBuildIds.Count == 0 ? "unknown" : string.Join(", ", prepared.Catalog.ClientBuildIds);
            Diag.Info(
                $"Prepared catalog from {prepared.Candidate.Source} release {prepared.Candidate.Version} (build {build}) with {catalog.HeaderCount} headers",
                "protocol");
            return true;
        }
        catch (Exception error)
        {
            lock (_gate)
                _header_catalog_errors[prepared.NormalizedPath] = error;
            Diag.Error(
                $"Unable to load prepared {prepared.Candidate.Version} headers: {error.Message}",
                "protocol");
            return false;
        }
    }

    HostServiceStatus HeaderStatus()
    {
        if (!_options.EnableClientMonitoring)
            return new HostServiceStatus(false, HostServicePhase.Disabled);
        if (_header_catalog_errors.Count > 0)
            return new HostServiceStatus(
                true,
                HostServicePhase.Faulted,
                _header_catalog_errors.Values.First());
        return Service(true, _header_started, _header_task);
    }

    static HostServiceStatus Service(bool enabled, bool started, Task task)
    {
        if (!enabled)
            return new HostServiceStatus(false, HostServicePhase.Disabled);
        if (!started)
            return new HostServiceStatus(true, HostServicePhase.Pending);
        if (!task.IsCompleted)
            return new HostServiceStatus(true, HostServicePhase.Running);
        if (task.IsCanceled)
            return new HostServiceStatus(true, HostServicePhase.Canceled);
        if (task.IsFaulted)
            return new HostServiceStatus(
                true,
                HostServicePhase.Faulted,
                task.Exception?.GetBaseException());
        return new HostServiceStatus(true, HostServicePhase.Completed);
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public ValueTask DisposeAsync()
    {
        Task disposal;
        lock (_gate)
        {
            _disposal ??= DisposeCoreAsync();
            disposal = _disposal;
        }
        return new ValueTask(disposal);
    }

    async Task DisposeCoreAsync()
    {
        Task transport;
        Task fallback;
        Task headers;
        Task? startup;
        HeaderCatalogCoordinator? coordinator;
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            startup = _startup;
            coordinator = HeaderCatalogs;
        }

        _lifetime.Cancel();
        if (HeaderCatalogs is not null)
            HeaderCatalogs.PreparationChanged -= HeaderPreparationChanged;

        if (coordinator is not null)
            await IgnoreAsync(coordinator.DisposeAsync().AsTask()).ConfigureAwait(false);
        if (startup is not null)
            await IgnoreAsync(startup).ConfigureAwait(false);
        lock (_gate)
        {
            transport = _transport_task;
            fallback = _fallback_task;
            headers = _header_task;
        }
        await Task.WhenAll(
            IgnoreAsync(transport),
            IgnoreAsync(fallback),
            IgnoreAsync(headers)).ConfigureAwait(false);

        if (coordinator is null && InstalledClients is not null)
            await InstalledClients.DisposeAsync().ConfigureAwait(false);
        application_runtime?.Dispose();
        if (Game is not null)
        {
            ((IDisposable)Game).Dispose();
            await Game.GameData.DisposeAsync().ConfigureAwait(false);
        }
        Extension?.Dispose();
        if (_owns_http)
            Http.Dispose();
        _lifetime.Dispose();
    }

    static async Task IgnoreAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
        }
    }
}

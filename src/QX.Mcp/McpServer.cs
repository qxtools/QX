using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text;
using static Qx.Mcp.McpArguments;
using static Qx.Mcp.McpSchema;

namespace Qx.Mcp;

public sealed class McpServer : IDisposable
{
    private const string ProtocolVersion = "2025-11-25";
    private const int AbandonGraceMs = 2000;
    private const int MaxOffset = 1000000;
    private const int ErrorAlreadyExists = 183;

    private static readonly HashSet<string> SupportedProtocolVersions =
        [.. McpProtocol.Supported];

    private static readonly string ServerVersion = ResolveVersion();

    private static readonly JsonSerializerOptions IndentedJson =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private static readonly McpToolAnnotations ClosedReadOnly = new(true, false, false, false);
    private static readonly McpToolAnnotations OpenReadOnly = new(true, false, false, true);
    private static readonly McpToolAnnotations OpenDestructiveWrite = new(false, true, false, true);

    private readonly McpServerOptions _options;
    private readonly List<McpTool> _tools;
    private readonly IReadOnlyList<McpTool> _listed_tools;
    private McpConfig _config;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private long _last_request_ticks;
    private readonly object _lifecycle = new();
    private bool _disposed;

    /// <summary>
    /// When an authenticated request was last served, or null if none has been.
    /// </summary>
    /// <remarks>
    /// The closest thing this server has to "a client is connected". MCP over HTTP has no session
    /// to hold open, so the only honest answer is when one last called — the window turns that
    /// into "active" for a while and back again, rather than claiming a connection it cannot see.
    /// </remarks>
    public DateTime? LastRequestUtc =>
        Volatile.Read(ref _last_request_ticks) is var ticks && ticks > 0
            ? new DateTime(ticks, DateTimeKind.Utc)
            : null;

    public int Port => _options.Port;
    public bool IsRunning => _listener?.IsListening == true;
    private bool EditorAvailable => RuntimeSupports(McpRuntimeCapability.Editor);
    private string Instructions => _options.Instructions?.Invoke() ?? string.Empty;

    /// <summary>
    /// The token and capability flags every request is checked against.
    /// </summary>
    /// <remarks>
    /// Replaceable while the server is listening, so a capability turned off in the interface takes
    /// effect on the next request rather than the next launch. The configuration itself stays
    /// immutable — a whole new one is swapped in, which is a single reference assignment and so
    /// cannot be read half-applied by a request already in flight.
    /// </remarks>
    public McpConfig Config
    {
        get => Volatile.Read(ref _config);
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            Volatile.Write(ref _config, value);
        }
    }

    /// <summary>The informational version reported to clients, read from the entry assembly.</summary>
    public static string Version => ServerVersion;

    /// <summary>The endpoint an MCP client should be configured with, including the access token.</summary>
    public string ClientUrl =>
        Config.RequireAuth
            ? $"http://127.0.0.1:{Port}/mcp?token={Config.Token}"
            : $"http://127.0.0.1:{Port}/mcp";

    public McpServer(McpServerOptions options, IEnumerable<McpTool> tools, McpConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(tools);
        options.Validate();
        _options = options;
        _config = config ?? (options.ConfigurationPath is { } path ? McpConfig.Load(path) : McpConfig.CreateDefault());
        _tools = BuildTools(tools);
        HashSet<string>? filter = _config.ToolFilter?.ToHashSet(StringComparer.Ordinal);
        _listed_tools = _tools
            .Where(tool => (filter?.Contains(tool.Name) ?? tool.Listed) || tool.Name is
                "get_server_info" or "list_mcp_tools" or "describe_mcp_tool" or "read_mcp_tool" or "call_mcp_tool")
            .OrderBy(tool => tool.Name, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Gets whether the last <see cref="Start"/> failed because the endpoint is already registered with
    /// http.sys, usually by another QX window. http.sys lists every HttpListener port as held by System
    /// (pid 4), so the owning process cannot be named.
    /// </summary>
    public bool PortRegistered { get; private set; }

    /// <summary>Gets why the last <see cref="Start"/> failed, or <see langword="null"/> when it succeeded.</summary>
    public string? StartFailure { get; private set; }

    public bool Start()
    {
        lock (_lifecycle)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (IsRunning)
                return true;
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{_options.Port}/mcp/");
            try
            {
                listener.Start();
                _listener = listener;
                _cts = new CancellationTokenSource();
                _ = AcceptLoop(listener, _cts.Token);
                PortRegistered = false;
                StartFailure = null;
                return true;
            }
            catch (HttpListenerException error)
            {
                listener.Close();
                PortRegistered = error.ErrorCode == ErrorAlreadyExists;
                StartFailure = PortRegistered
                    ? $"MCP port {_options.Port} is already registered."
                    : $"MCP port {_options.Port} is held by {PortHolder(_options.Port)}.";
                return false;
            }
        }
    }

    public void Stop()
    {
        HttpListener? listener;
        CancellationTokenSource? cancellation;
        lock (_lifecycle)
        {
            listener = _listener;
            cancellation = _cts;
            _listener = null;
            _cts = null;
        }
        try
        {
            cancellation?.Cancel();
        }
        finally
        {
            listener?.Close();
            cancellation?.Dispose();
        }
    }

    public void Dispose()
    {
        lock (_lifecycle)
            _disposed = true;
        Stop();
    }

    public static string PortHolder(int port)
    {
        if (!OperatingSystem.IsWindows())
            return "another process";

        try
        {
            int pid = owning_pid(port);
            if (pid <= 0)
                return "another process";
            using System.Diagnostics.Process holder = System.Diagnostics.Process.GetProcessById(pid);
            return $"{holder.ProcessName} (pid {pid})";
        }
        catch
        {
            return "another process";
        }
    }

    [System.Runtime.InteropServices.DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        nint table, ref int size, bool order, int af, int tableClass, int reserved);

    private static int owning_pid(int port)
    {
        const int afInet = 2;
        const int tcpTableOwnerPidListener = 3;

        int size = 0;
        GetExtendedTcpTable(0, ref size, false, afInet, tcpTableOwnerPidListener, 0);
        nint buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
        try
        {
            if (GetExtendedTcpTable(buffer, ref size, false, afInet, tcpTableOwnerPidListener, 0) != 0)
                return 0;

            int rows = System.Runtime.InteropServices.Marshal.ReadInt32(buffer);
            nint row = buffer + 4;
            for (int i = 0; i < rows; i++)
            {
                int localPort = System.Runtime.InteropServices.Marshal.ReadInt32(row, 8);
                int resolved = ((localPort & 0xFF) << 8) | ((localPort >> 8) & 0xFF);
                if (resolved == port)
                    return System.Runtime.InteropServices.Marshal.ReadInt32(row, 20);
                row += 24;
            }
            return 0;
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);
        }
    }

    private async Task AcceptLoop(HttpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener is { IsListening: true })
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch
            {
                break;
            }

            _ = HandleRequest(context, cancellationToken);
        }
    }

    private async Task HandleRequest(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            HttpListenerRequest request = context.Request;
            if (!IsLoopbackHost(request.Headers["Host"] ?? request.UserHostName))
            {
                CloseResponse(context.Response, HttpStatusCode.Forbidden);
                return;
            }

            if (!IsAllowedOrigin(request.Headers["Origin"]))
            {
                CloseResponse(context.Response, HttpStatusCode.Forbidden);
                return;
            }

            if (request.HttpMethod != "POST")
            {
                context.Response.Headers[HttpResponseHeader.Allow] = "POST";
                CloseResponse(context.Response, HttpStatusCode.MethodNotAllowed);
                return;
            }

            if (Config.RequireAuth && !Config.MatchesToken(PresentedToken(request)))
            {
                context.Response.AddHeader("WWW-Authenticate", "Bearer realm=\"QX MCP\"");
                CloseResponse(context.Response, HttpStatusCode.Unauthorized);
                return;
            }

            // Stamped only once a caller has got past the token, so a port scan cannot make the
            // window claim an agent is attached.
            Volatile.Write(ref _last_request_ticks, DateTime.UtcNow.Ticks);

            string? protocolVersion = request.Headers["MCP-Protocol-Version"];
            if (!string.IsNullOrWhiteSpace(protocolVersion) && !SupportedProtocolVersions.Contains(protocolVersion))
            {
                // Answered as a modern error rather than a bare status, because that body is how a
                // dual-era client tells a modern server from one that simply does not speak MCP.
                await WriteJsonResponse(
                    context.Response,
                    HttpStatusCode.BadRequest,
                    Error(default, McpProtocol.UnsupportedProtocolVersion, "Unsupported protocol version",
                        new Dictionary<string, object?>
                        {
                            ["supported"] = McpProtocol.Supported,
                            ["requested"] = protocolVersion
                        }),
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            string body;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8))
                body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

            using JsonDocument doc = JsonDocument.Parse(body);

            if (doc.RootElement.ValueKind == JsonValueKind.Object && McpProtocol.IsModern(doc.RootElement))
            {
                await ServeModern(context, doc.RootElement, request.Headers.Get, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            var responses = new List<string>();
            bool isBatch = doc.RootElement.ValueKind == JsonValueKind.Array;
            bool isEmptyBatch = false;

            if (isBatch)
            {
                string effectiveProtocolVersion = string.IsNullOrWhiteSpace(protocolVersion)
                    ? "2025-03-26"
                    : protocolVersion;
                if (effectiveProtocolVersion != "2025-03-26")
                {
                    CloseResponse(context.Response, HttpStatusCode.BadRequest);
                    return;
                }

                isEmptyBatch = doc.RootElement.GetArrayLength() == 0;
                if (isEmptyBatch)
                    responses.Add(Error(default, -32600, "Invalid Request"));

                foreach (JsonElement element in doc.RootElement.EnumerateArray())
                {
                    string? r = await Dispatch(element, cancellationToken).ConfigureAwait(false);
                    if (r is not null)
                        responses.Add(r);
                }
            }
            else
            {
                string? response = await Dispatch(doc.RootElement, cancellationToken).ConfigureAwait(false);
                if (response is not null)
                    responses.Add(response);
            }

            if (responses.Count == 0)
            {
                CloseResponse(context.Response, HttpStatusCode.Accepted);
                return;
            }

            string responseJson = isBatch && !isEmptyBatch
                ? "[" + string.Join(",", responses) + "]"
                : responses[0];
            await WriteJsonResponse(context.Response, HttpStatusCode.OK, responseJson, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            try
            {
                string responseJson = Error(default, -32700, "Parse error");
                await WriteJsonResponse(context.Response, HttpStatusCode.BadRequest, responseJson, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                context.Response.Abort();
            }
        }
        catch (OperationCanceledException)
        {
            context.Response.Abort();
        }
        catch
        {
            try { CloseResponse(context.Response, HttpStatusCode.InternalServerError); } catch { }
        }
    }

    /// <summary>
    /// Answers one stateless request of revision 2026-07-28.
    /// </summary>
    /// <remarks>
    /// Everything the request needs to be understood is in the request, so nothing is looked up and
    /// nothing is kept. The checks in front of the dispatch are the revision's own: the metadata
    /// has to be complete, the version has to be one this server answers, and the mirrored headers
    /// have to agree with the body, because an intermediary may have routed on them.
    /// </remarks>
    private async Task ServeModern(
        HttpListenerContext context,
        JsonElement request,
        Func<string, string?> header,
        CancellationToken cancellationToken)
    {
        JsonElement id = request.TryGetProperty("id", out JsonElement identifier) && IsValidId(identifier)
            ? identifier
            : default;

        McpRejection? rejection = McpProtocol.ReadContext(request, out McpRequestContext protocol)
            ?? McpProtocol.CheckHeaders(request, protocol.ProtocolVersion, header);

        if (rejection is null &&
            request.TryGetProperty("method", out JsonElement methodField) &&
            methodField.ValueKind == JsonValueKind.String &&
            !Answers(methodField.GetString()!))
        {
            rejection = new McpRejection(-32601, $"Method not found: {methodField.GetString()}");
        }

        if (rejection is { } refusal)
        {
            await WriteJsonResponse(
                context.Response,
                (HttpStatusCode)McpProtocol.StatusFor(refusal.Code),
                Error(id, refusal.Code, refusal.Message, refusal.Data),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        string? response = await Dispatch(request, protocol, cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            CloseResponse(context.Response, HttpStatusCode.Accepted);
            return;
        }

        await WriteJsonResponse(context.Response, HttpStatusCode.OK, response, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Whether this server implements a method at all, whatever the era.</summary>
    private static bool Answers(string method) => method is
        "server/discover" or "ping" or "tools/list" or "tools/call" or "initialize";

    private Task<string?> Dispatch(JsonElement request, CancellationToken cancellationToken) =>
        Dispatch(request, McpRequestContext.ForLegacy(ProtocolVersion), cancellationToken);

    private async Task<string?> Dispatch(
        JsonElement request,
        McpRequestContext protocol,
        CancellationToken cancellationToken)
    {
        if (request.ValueKind != JsonValueKind.Object)
            return Error(default, -32600, "Invalid Request");

        if (!request.TryGetProperty("jsonrpc", out JsonElement jsonrpc) ||
            jsonrpc.ValueKind != JsonValueKind.String ||
            jsonrpc.GetString() != "2.0")
        {
            return Error(default, -32600, "Invalid Request");
        }

        bool hasId = request.TryGetProperty("id", out JsonElement id);
        if (hasId && !IsValidId(id))
            return Error(default, -32600, "Invalid Request");

        if (!request.TryGetProperty("method", out JsonElement methodElement) ||
            methodElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(methodElement.GetString()))
        {
            if (request.TryGetProperty("error", out _) || hasId && request.TryGetProperty("result", out _))
                return null;
            return Error(hasId ? id : default, -32600, "Invalid Request");
        }

        string method = methodElement.GetString()!;
        if (method.StartsWith("notifications/", StringComparison.Ordinal))
            return hasId ? Error(id, -32600, "Invalid Request") : null;

        if (!hasId)
            return null;

        switch (method)
        {
            case "initialize":
                return Result(id, new Dictionary<string, object?>
                {
                    ["protocolVersion"] = NegotiatedProtocolVersion(request),
                    ["capabilities"] = new Dictionary<string, object?>
                    {
                        ["tools"] = new Dictionary<string, object?> { ["listChanged"] = false }
                    },
                    ["serverInfo"] = new Dictionary<string, object?>
                    {
                        ["name"] = _options.Name,
                        ["title"] = _options.Title ?? _options.Name,
                        ["version"] = _options.Version ?? ServerVersion
                    },
                    ["instructions"] = Instructions
                });

            case "server/discover":
                return Result(id, protocol, new Dictionary<string, object?>
                {
                    ["supportedVersions"] = McpProtocol.Supported,
                    ["capabilities"] = new Dictionary<string, object?>
                    {
                        ["tools"] = new Dictionary<string, object?> { ["listChanged"] = false }
                    },
                    ["instructions"] = Instructions,
                    ["ttlMs"] = 3600000,
                    ["cacheScope"] = "private"
                });

            case "ping":
                return Result(id, protocol, new Dictionary<string, object?>());

            case "tools/list":
                return Result(id, protocol, new Dictionary<string, object?>
                {
                    ["tools"] = _listed_tools.Select(ToolDefinition).ToList(),
                    ["ttlMs"] = 3600000,
                    ["cacheScope"] = "private"
                });

            case "tools/call":
                return await CallTool(id, protocol, request, cancellationToken).ConfigureAwait(false);

            default:
                return hasId ? Error(id, -32601, $"Method not found: {method}") : null;
        }
    }

    private async Task<string> CallTool(
        JsonElement id,
        McpRequestContext protocol,
        JsonElement request,
        CancellationToken cancellationToken)
    {
        if (!request.TryGetProperty("params", out JsonElement parameters) || parameters.ValueKind != JsonValueKind.Object)
            return Error(id, -32602, "Invalid params");

        if (!parameters.TryGetProperty("name", out JsonElement nameElement) ||
            nameElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(nameElement.GetString()))
        {
            return Error(id, -32602, "Invalid params");
        }

        string name = nameElement.GetString()!;
        JsonElement args = parameters.TryGetProperty("arguments", out JsonElement arguments) ? arguments : default;
        if (args.ValueKind != JsonValueKind.Undefined && args.ValueKind != JsonValueKind.Object)
            return Error(id, -32602, "Invalid params");

        McpTool? tool = _tools.FirstOrDefault(t => t.Name == name);
        if (tool is null)
            return Error(id, -32602, $"Unknown tool: {name}");

        return await Invoke(id, protocol, tool, args, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> Invoke(
        JsonElement id,
        McpRequestContext protocol,
        McpTool tool,
        JsonElement args,
        CancellationToken cancellationToken)
    {
        CancellationTokenSource scope = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bool abandoned = false;
        try
        {
            RequireAllowed(tool);
            RequireArguments(tool, args);
            int timeout_ms = tool.Timeout?.Invoke(args) ?? 0;
            Task<string> execution = tool.Handler(args, scope.Token);
            if (timeout_ms <= 0)
            {
                string text = await execution.ConfigureAwait(false);
                return Result(id, protocol, ToolContent(tool, text));
            }

            scope.CancelAfter(timeout_ms);
            try
            {
                string text = await execution
                    .WaitAsync(TimeSpan.FromMilliseconds((double)timeout_ms + AbandonGraceMs), cancellationToken)
                    .ConfigureAwait(false);
                return Result(id, protocol, ToolContent(tool, text));
            }
            catch (TimeoutException)
            {
                abandoned = true;
                Abandon(execution, scope);
                return Result(id, protocol, ToolContent(
                    $"'{tool.Name}' exceeded its {timeout_ms} ms timeout and did not stop when cancelled. " +
                    "The extension operation has not stopped. Cancellation is cooperative.",
                    true));
            }
        }
        catch (OperationCanceledException) when (
            scope.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return Result(id, protocol, ToolContent($"'{tool.Name}' was cancelled after its timeout expired.", true));
        }
        catch (McpToolException error)
        {
            return Result(id, protocol, ToolContent(error.Message, true, error.Metadata));
        }
        catch (Exception error)
        {
            return Result(id, protocol, ToolContent(error.Message, true));
        }
        finally
        {
            if (!abandoned)
                scope.Dispose();
        }
    }

    private static void Abandon(Task<string> execution, CancellationTokenSource scope) =>
        _ = execution.ContinueWith(
            static (completed, state) =>
            {
                _ = completed.Exception;
                ((CancellationTokenSource)state!).Dispose();
            },
            scope,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

    private static Dictionary<string, object?> ToolContent(
        string text,
        bool isError,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        var content = new Dictionary<string, object?>
        {
            ["content"] = new List<object?> { new Dictionary<string, object?> { ["type"] = "text", ["text"] = text } },
            ["isError"] = isError
        };
        if (metadata is { Count: > 0 })
            content["_meta"] = metadata;
        return content;
    }

    private static Dictionary<string, object?> ToolContent(McpTool tool, string text)
    {
        if (tool.OutputSchema is null)
            return ToolContent(text, false);
        JsonElement output = StructuredOutput(tool, text);
        if (!ObjectOutput(tool))
            output = JsonSerializer.SerializeToElement(new { result = output });
        Dictionary<string, object?> content = ToolContent(output.GetRawText(), false);
        content["structuredContent"] = output;
        return content;
    }

    private static bool ObjectOutput(McpTool tool)
    {
        JsonElement schema = JsonSerializer.SerializeToElement(tool.OutputSchema);
        return schema.ValueKind is JsonValueKind.Object &&
            schema.TryGetProperty("type", out JsonElement type) &&
            type.ValueKind is JsonValueKind.String && type.GetString() == "object";
    }

    private static JsonElement StructuredOutput(McpTool tool, string text)
    {
        using JsonDocument document = JsonDocument.Parse(text);
        if (ObjectOutput(tool) && document.RootElement.ValueKind is not JsonValueKind.Object)
            throw new JsonException($"MCP tool '{tool.Name}' returned a non-object structured result.");
        return document.RootElement.Clone();
    }

    private static object StructuredSchema(McpTool tool) => ObjectOutput(tool)
        ? tool.OutputSchema!
        : new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object?> { ["result"] = tool.OutputSchema },
            ["required"] = new[] { "result" },
            ["additionalProperties"] = false
        };

    private List<McpTool> BuildTools(IEnumerable<McpTool>? additional_tools)
    {
        var tools = new List<McpTool>
        {
        new McpTool
        {
            Name = "get_server_info",
            Description = "Get this MCP server's identity and policy: version, protocol versions, endpoint, tool count and which capabilities are enabled.",
            InputSchema = Schema(),
            Annotations = ClosedReadOnly,
            Handler = (args, ct) => Task.FromResult(ServerInfoJson())
        },
        new McpTool
        {
            Name = "list_mcp_tools",
            Description = "Search all MCP tools, including filtered tools, with their title, permissions and parameter names. Use describe_mcp_tool for the full schema and read_mcp_tool or call_mcp_tool to call a filtered tool. Follow nextOffset for more results.",
            InputSchema = OptionalSchema(
                ("filter", "string", "optional name, title or description substring", null, null, null),
                ("limit", "integer", "maximum returned tools", 50, 1, 100),
                OffsetProperty("tool")),
            Annotations = ClosedReadOnly,
            Handler = (args, ct) => Task.FromResult(ToolCatalogJson(
                Str(args, "filter"), Int(args, "limit", 50), Int(args, "offset")))
        },
        new McpTool
        {
            Name = "describe_mcp_tool",
            Description = "Get one MCP tool's complete input/output schemas, annotations and permissions, including tools hidden from discovery.",
            InputSchema = Schema(("name", "string", "exact tool name from list_mcp_tools")),
            Annotations = ClosedReadOnly,
            Handler = (args, ct) => Task.FromResult(JsonSerializer.Serialize(
                ToolDetails(FindTool(Str(args, "name"))), IndentedJson))
        },
        RoutedTool(read_only: true),
        RoutedTool(read_only: false)
        };

        if (additional_tools is not null)
            tools.AddRange(additional_tools);

        if (tools.Any(tool => tool is null))
            throw new InvalidOperationException("MCP tools cannot contain null entries.");
        if (tools.Any(tool => string.IsNullOrWhiteSpace(tool.Name)))
            throw new InvalidOperationException("MCP tool names cannot be empty.");

        string[] duplicates = tools
            .GroupBy(tool => tool.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (duplicates.Length != 0)
        {
            throw new InvalidOperationException(
                $"MCP tool names must be unique: {string.Join(", ", duplicates)}.");
        }

        return AvailableTools(tools);
    }

    private static Dictionary<string, object?> ToolDefinition(McpTool tool)
    {
        var definition = new Dictionary<string, object?>
        {
            ["name"] = tool.Name,
            ["description"] = tool.Description,
            ["inputSchema"] = tool.InputSchema,
            ["annotations"] = new Dictionary<string, object?>
            {
                ["readOnlyHint"] = tool.Annotations.ReadOnlyHint,
                ["destructiveHint"] = tool.Annotations.DestructiveHint,
                ["idempotentHint"] = tool.Annotations.IdempotentHint,
                ["openWorldHint"] = tool.Annotations.OpenWorldHint
            }
        };
        if (!string.IsNullOrWhiteSpace(tool.Title))
            definition["title"] = tool.Title;
        if (tool.OutputSchema is not null)
            definition["outputSchema"] = StructuredSchema(tool);
        if (tool.Metadata is not null)
            definition["_meta"] = tool.Metadata;
        return definition;
    }

    private McpTool RoutedTool(bool read_only) => new()
    {
        Name = read_only ? "read_mcp_tool" : "call_mcp_tool",
        Description = read_only
            ? "Call any read-only MCP tool by name, even if filtered from discovery. Read its schema with describe_mcp_tool first. Target permissions and timeout still apply. Returns the target output in result. Rejects write tools."
            : "Call any MCP tool by name, even if filtered from discovery. Read its schema with describe_mcp_tool first. Target permissions and timeout still apply. May modify files, editor or game state. Returns the target output in result.",
        InputSchema = MixedSchema(
            [("name", "string", "exact tool name from list_mcp_tools")],
            ("arguments", "object", "arguments matching the target tool's input schema", null, null, null)),
        OutputSchema = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object?> { ["result"] = new Dictionary<string, object?>() },
            ["required"] = new[] { "result" }
        },
        Annotations = read_only ? OpenReadOnly : OpenDestructiveWrite,
        Timeout = args =>
        {
            (McpTool tool, JsonElement arguments) = RoutedTarget(args, read_only);
            return tool.Timeout?.Invoke(arguments) ?? 0;
        },
        Handler = async (args, cancellation_token) =>
        {
            (McpTool tool, JsonElement arguments) = RoutedTarget(args, read_only);
            string text = await tool.Handler(arguments, cancellation_token).ConfigureAwait(false);
            object output = text;
            try
            {
                output = StructuredOutput(tool, text);
            }
            catch (JsonException) when (tool.OutputSchema is null)
            {
            }
            return JsonSerializer.Serialize(new { result = output });
        }
    };

    private (McpTool Tool, JsonElement Arguments) RoutedTarget(JsonElement args, bool read_only)
    {
        string name = Str(args, "name");
        if (name is "read_mcp_tool" or "call_mcp_tool")
            throw new McpToolException("Pass the target tool name directly; nested routing is not supported.");
        McpTool tool = FindTool(name);
        if (read_only && !tool.Annotations.ReadOnlyHint)
            throw new McpToolException($"'{name}' is not read-only; use call_mcp_tool instead.");
        RequireAllowed(tool);
        JsonElement arguments = args.TryGetProperty("arguments", out JsonElement value)
            ? value
            : JsonSerializer.SerializeToElement(new Dictionary<string, object?>());
        if (arguments.ValueKind != JsonValueKind.Object)
            throw new McpToolException("arguments must be an object.");
        RequireArguments(tool, arguments);
        return (tool, arguments);
    }

    private McpTool FindTool(string name) =>
        _tools.FirstOrDefault(tool => tool.Name == name)
            ?? throw new McpToolException($"Unknown tool: {name}");

    private void RequireAllowed(McpTool tool)
    {
        IReadOnlyList<string> missing = Config.MissingCapabilities(tool.Capability);
        if (missing.Count > 0)
            throw new McpToolException(
                $"'{tool.Name}' is disabled: enable {string.Join(" and ", missing)} in MCP settings.");
    }

    private static void RequireArguments(McpTool tool, JsonElement args)
    {
        JsonElement schema = JsonSerializer.SerializeToElement(tool.InputSchema);
        if (schema.ValueKind != JsonValueKind.Object)
            return;
        if (schema.TryGetProperty("required", out JsonElement required))
        {
            foreach (JsonElement entry in required.EnumerateArray())
            {
                string name = entry.GetString()!;
                if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out _))
                    throw new McpToolException($"missing required parameter '{name}' for '{tool.Name}'");
            }
        }
        if (args.ValueKind != JsonValueKind.Object ||
            !schema.TryGetProperty("additionalProperties", out JsonElement additional) ||
            additional.ValueKind != JsonValueKind.False)
        {
            return;
        }
        IReadOnlyCollection<string> valid = PropertyNames(tool.InputSchema);
        foreach (JsonProperty argument in args.EnumerateObject())
        {
            if (valid.Contains(argument.Name))
                continue;
            throw new McpToolException(valid.Count == 0
                ? $"unknown parameter '{argument.Name}'; '{tool.Name}' takes no parameters"
                : $"unknown parameter '{argument.Name}'; valid: {string.Join(", ", valid)}");
        }
    }

    private Dictionary<string, object?> ToolDetails(McpTool tool)
    {
        Dictionary<string, object?> details = ToolDefinition(tool);
        details["requires"] = CapabilityNames(tool.Capability);
        details["runtimeRequires"] = RuntimeCapabilityNames(tool.RuntimeCapability);
        details["allowed"] = Config.Allows(tool.Capability);
        details["listed"] = _listed_tools.Contains(tool);
        return details;
    }

    private List<McpTool> AvailableTools(List<McpTool> tools) =>
        tools
            .Where(tool => RuntimeSupports(tool.RuntimeCapability))
            .ToList();

    private static string Result(JsonElement id, object result) =>
        JsonSerializer.Serialize(new Dictionary<string, object?> { ["jsonrpc"] = "2.0", ["id"] = IdValue(id), ["result"] = result });

    /// <summary>
    /// Serializes a result, stamping it the way the request's era expects.
    /// </summary>
    /// <remarks>
    /// A modern result says what kind it is and who answered, because the client is holding no
    /// session to look either up in. A legacy result is left exactly as it was, since an older
    /// client validates against a schema that has no room for the extra fields.
    /// </remarks>
    private string Result(JsonElement id, McpRequestContext context, Dictionary<string, object?> result)
    {
        if (context.Era is McpEra.Modern)
        {
            result["resultType"] = "complete";
            result["_meta"] = new Dictionary<string, object?>
            {
                [McpProtocol.MetaServerInfo] = new Dictionary<string, object?>
                {
                    ["name"] = _options.Name,
                    ["version"] = _options.Version ?? ServerVersion
                }
            };
        }
        return Result(id, result);
    }

    private static string Error(JsonElement id, int code, string message) =>
        Error(id, code, message, null);

    private static string Error(JsonElement id, int code, string message, object? data)
    {
        var error = new Dictionary<string, object?> { ["code"] = code, ["message"] = message };
        if (data is not null)
            error["data"] = data;
        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = IdValue(id),
            ["error"] = error
        });
    }

    private static bool IsValidId(JsonElement id) => id.ValueKind switch
    {
        JsonValueKind.Number => id.TryGetInt64(out _),
        JsonValueKind.String => true,
        _ => false
    };

    private static object? IdValue(JsonElement id)
    {
        if (id.ValueKind == JsonValueKind.String)
            return id.GetString();
        if (id.ValueKind == JsonValueKind.Number && id.TryGetInt64(out long value))
            return value;
        return null;
    }

    private static string NegotiatedProtocolVersion(JsonElement request)
    {
        if (request.TryGetProperty("params", out JsonElement parameters) &&
            parameters.ValueKind == JsonValueKind.Object &&
            parameters.TryGetProperty("protocolVersion", out JsonElement requested) &&
            requested.ValueKind == JsonValueKind.String &&
            SupportedProtocolVersions.Contains(requested.GetString()!) &&
            // The modern revision has no initialize, so a session can never be opened on it.
            requested.GetString() != McpProtocol.Modern)
        {
            return requested.GetString()!;
        }

        return ProtocolVersion;
    }

    private string ServerInfoJson() =>
        JsonSerializer.Serialize(
            new Dictionary<string, object?>
            {
                ["name"] = _options.Name,
                ["version"] = _options.Version ?? ServerVersion,
                ["protocolVersion"] = ProtocolVersion,
                ["supportedProtocolVersions"] = SupportedProtocolVersions
                    .OrderByDescending(version => version, StringComparer.Ordinal)
                    .ToArray(),
                ["endpoint"] = $"http://127.0.0.1:{Port}/mcp",
                ["listening"] = IsRunning,
                ["toolCount"] = _tools.Count,
                ["listedToolCount"] = _listed_tools.Count,
                ["configPath"] = _options.ConfigurationPath,
                ["authRequired"] = Config.RequireAuth,
                ["capabilities"] = new Dictionary<string, object?>
                {
                    ["allowExecute"] = Config.AllowExecute,
                    ["allowFileWrite"] = Config.AllowFileWrite,
                    ["allowEditor"] = Config.AllowEditor && EditorAvailable
                },
                ["runtimeCapabilities"] = RuntimeCapabilityNames(_options.RuntimeCapabilities?.Invoke() ?? McpRuntimeCapability.None),

            },
            IndentedJson);

    private string ToolCatalogJson(string filter, int limit, int offset)
    {
        limit = Math.Clamp(limit, 1, 100);
        offset = Math.Clamp(offset, 0, MaxOffset);
        McpTool[] matches = _tools
            .Where(tool =>
                string.IsNullOrWhiteSpace(filter) ||
                tool.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                tool.Title?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true ||
                tool.Description.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(tool => tool.Name, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, object?>[] page = [.. matches.Skip(offset).Take(limit).Select(CatalogRow)];
        return JsonSerializer.Serialize(new
        {
            tools = page,
            total = matches.Length,
            nextOffset = offset + page.Length < matches.Length ? (int?)(offset + page.Length) : null
        },
            IndentedJson);
    }

    private Dictionary<string, object?> CatalogRow(McpTool tool)
    {
        var row = new Dictionary<string, object?> { ["name"] = tool.Name };
        if (!string.IsNullOrWhiteSpace(tool.Title))
            row["title"] = tool.Title;
        row["description"] = tool.Description;
        row["parameters"] = ParameterNames(tool.InputSchema);
        row["requires"] = CapabilityNames(tool.Capability);
        row["runtimeRequires"] = RuntimeCapabilityNames(tool.RuntimeCapability);
        row["allowed"] = Config.Allows(tool.Capability);
        row["listed"] = _listed_tools.Contains(tool);
        row["readOnly"] = tool.Annotations.ReadOnlyHint;
        row["destructive"] = tool.Annotations.DestructiveHint;
        return row;
    }

    private static string[] CapabilityNames(McpCapability capability) =>
        capability == McpCapability.None
            ? []
            : Enum.GetValues<McpCapability>()
                .Where(value => value != McpCapability.None && capability.HasFlag(value))
                .Select(value => value.ToString())
                .ToArray();

    private bool RuntimeSupports(McpRuntimeCapability capability) =>
        ((_options.RuntimeCapabilities?.Invoke() ?? McpRuntimeCapability.None) & capability) == capability;

    private static string[] RuntimeCapabilityNames(McpRuntimeCapability capability) =>
        capability == McpRuntimeCapability.None
            ? []
            : Enum.GetValues<McpRuntimeCapability>()
                .Where(value => value != McpRuntimeCapability.None && capability.HasFlag(value))
                .Select(value => value.ToString())
                .ToArray();

    private static string[] ParameterNames(object schema)
    {
        string[] required = schema is Dictionary<string, object?> map &&
            map.TryGetValue("required", out object? names) &&
            names is string[] list
                ? list
                : [];
        return PropertyNames(schema)
            .Select(name => required.Contains(name, StringComparer.Ordinal) ? name : name + "?")
            .ToArray();
    }

    private static IReadOnlyCollection<string> PropertyNames(object schema) =>
        schema is Dictionary<string, object?> map &&
        map.TryGetValue("properties", out object? raw) &&
        raw is Dictionary<string, object?> properties
            ? properties.Keys
            : [];

    private static string ResolveVersion()
    {
        Assembly assembly = Assembly.GetEntryAssembly() ?? typeof(McpServer).Assembly;
        string? version = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (string.IsNullOrWhiteSpace(version))
            version = assembly.GetName().Version?.ToString();
        if (string.IsNullOrWhiteSpace(version))
            return "0.0.0";

        int metadata = version.IndexOf('+');
        return metadata > 0 ? version[..metadata] : version;
    }

    private static string? PresentedToken(HttpListenerRequest request)
    {
        string? header = request.Headers["X-MCP-Token"];
        if (!string.IsNullOrWhiteSpace(header))
            return header.Trim();

        string? authorization = request.Headers["Authorization"];
        if (!string.IsNullOrWhiteSpace(authorization))
        {
            const string scheme = "Bearer ";
            if (authorization.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
                return authorization[scheme.Length..].Trim();
        }

        string? query = request.QueryString["token"];
        return string.IsNullOrWhiteSpace(query) ? null : query.Trim();
    }

    /// <summary>True when an HTTP Host header names a loopback address, the only accepted target.</summary>
    public static bool IsLoopbackHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        if (!Uri.TryCreate($"http://{host}/", UriKind.Absolute, out Uri? uri))
            return false;

        string name = uri.Host.Trim('[', ']');
        if (name.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        return IPAddress.TryParse(name, out IPAddress? address) && IPAddress.IsLoopback(address);
    }

    private static bool IsAllowedOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
            return true;

        if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri))
            return false;

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        return IPAddress.TryParse(uri.Host, out IPAddress? address) && IPAddress.IsLoopback(address);
    }

    private static async Task WriteJsonResponse(
        HttpListenerResponse response,
        HttpStatusCode statusCode,
        string json,
        CancellationToken cancellationToken)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        response.StatusCode = (int)statusCode;
        response.ContentType = "application/json";
        response.ContentEncoding = Encoding.UTF8;
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        response.Close();
    }

    private static void CloseResponse(HttpListenerResponse response, HttpStatusCode statusCode)
    {
        response.StatusCode = (int)statusCode;
        response.ContentLength64 = 0;
        response.Close();
    }
}

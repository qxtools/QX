using Qx.Hosting;
using Qx.Interception.GEarth;
using Qx.Mcp;
using McpRoomTools;

using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    stop.Cancel();
};

await using var host = new GameHost(new GameHostOptions
{
    GEarth = GEarthOptions.Parse(args, new GEarthOptions
    {
        Title = "MCP Room Tools",
        Author = "QX",
        Description = "Search room furniture and request movement through MCP.",
        Version = "1.0.0"
    }),
    ReconnectTransport = true
});

using var server = new McpServer(new McpServerOptions
{
    Name = "MCP Room Tools",
    Port = 9401,
    Instructions = () => "Read room_overview first. find_furniture returns matching furniture and the current room ID. walk_to_furniture requires that room ID and rechecks the target before sending movement. A sent request does not confirm arrival.",
    ConfigurationPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "QX Examples", "McpRoomTools", "mcp.json")
}, RoomTools.Create(host));

try
{
    if (!server.Start())
        throw new InvalidOperationException(server.StartFailure);
    Console.WriteLine($"MCP: {server.ClientUrl}");
    await host.StartAsync(stop.Token);
    await host.TransportTask.WaitAsync(stop.Token);
}
catch (OperationCanceledException) when (stop.IsCancellationRequested)
{
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    Environment.ExitCode = 1;
}

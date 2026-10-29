# Getting started

Install the .NET SDK version listed in the repository's `global.json`, then create a console extension:

```sh
dotnet new console -n MyExtension -f net10.0
cd MyExtension
dotnet add package QX.Hosting --version 0.1.0
```

Replace `Program.cs` with:

```csharp
using Qx.Hosting;
using Qx.Interception.GEarth;

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
        Title = "My Extension",
        Description = "Shows room information.",
        Author = "Your name",
        Version = "1.0.0"
    }),
    ReconnectTransport = true
});

host.Game.Room.Ready += () =>
{
    var room = host.Queries.Room();
    Console.WriteLine($"Room {room.Data?.Id}: {room.Data?.FloorItemCount} floor items.");
};

try
{
    await host.StartAsync(stop.Token);
    await host.TransportTask.WaitAsync(stop.Token);
}
catch (OperationCanceledException) when (stop.IsCancellationRequested)
{
}
```

Start G-Earth, enable extensions and run:

```sh
dotnet run -- -p 9092
```

Use the extension port configured in G-Earth. Enter or reload a room after connecting so QX receives
the room state. `StartAsync` starts the host; it does not mean a hotel session or room is ready.
Ctrl+C disconnects and disposes the host.

For complete UI, chat-command and MCP projects, see [Building an extension](extensions.md).
For actions and their typed inputs, use the [Game API](application.md) and [API reference](../api/index.md).

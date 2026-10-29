# Getting started

Install the .NET SDK version listed in the repository's `global.json`, then create a console extension
and add QX from NuGet:

```sh
dotnet new console -n MyExtension -f net10.0
cd MyExtension
dotnet add package QX --version 0.1.1
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

## NuGet packages

| Package | Use |
| --- | --- |
| `QX` | Normal extensions: G-Earth, game state, actions, data and Wired. |
| `QX.Core` | Low-level packets, models and interception. Installed automatically with `QX`. |
| `QX.Mcp` | Add an MCP server and native game tools. |
| `QX.Scripting` | Compile and run C# scripts. |

Install `QX` first. Add the optional packages only when needed, using the same version:

```sh
dotnet add package QX.Mcp --version 0.1.1
dotnet add package QX.Scripting --version 0.1.1
```

These packages contain the individual framework DLLs. Names such as `Qx.Hosting` and
`Qx.Game` in the API reference are namespaces, not additional packages you must install.

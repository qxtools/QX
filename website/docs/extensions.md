# Building an extension

QX runs the connection and game state. Your extension owns its interface and behavior.
These are complete, runnable projects in the [examples folder](https://github.com/qxscripter/QX/tree/main/examples).

Requires G-Earth and the .NET SDK version in `global.json`.

```sh
git clone https://github.com/qxscripter/QX.git
cd QX
```

Start G-Earth first. Enter or reload a room after connecting an extension so it receives the
full room state. Change `9092` below if G-Earth uses a different extension port.

## Room Inspector: a desktop extension

```sh
dotnet run --project examples/RoomInspector -- -p 9092
```

The window lists floor furniture as it arrives. Search by name, class name or ID; select an item
to inspect its state, position and owner. **Walk to tile** requests movement to its current position.
**Use furniture** sends one use request. **Export results** saves the filtered list as JSON.

The export includes room identity and completeness metadata. Loading or capped results are
identified instead of being presented as a complete room. Removed furniture and room changes
are checked again when an action is clicked.

The example uses Avalonia for its window. QX has no dependency on Avalonia.

| File | Responsibility |
| --- | --- |
| `Program.cs` | Configure the desktop app and create the game host. |
| `InspectorWindow.cs` | Subscribe to room changes, filter snapshots, run item actions and export. |
| `RoomInspector.csproj` | Reference QX and the UI packages. |

Room events mark the list for refresh. A timer combines bursts of events into one refresh,
instead of rebuilding the list for every individual furniture update. Clicking the extension
in G-Earth brings its window forward.

## Chat Commands: intercept local commands

```sh
dotnet run --project examples/ChatCommands -- -p 9092
```

Type commands into the hotel's normal chat box. The extension intercepts `.qx` commands
before they leave the client. Their output appears in the extension's console, not in room chat.
Ordinary chat is passed through unchanged.

| Command | Result |
| --- | --- |
| `.qx help` | Show commands. |
| `.qx room` | Show the current room and item counts. |
| `.qx find chair` | Search observed furniture; show IDs, coordinates and states. |
| `.qx go 5 8` | Request movement to a tile. |
| `.qx use 12345` | Use one floor item after checking it is still in the room. |
| `.qx export` | Export the observed furniture and completeness metadata. |

Exports go to `QX Examples/ChatCommands/Exports` below the operating system's local application
data directory. The exact filename is printed after saving. Existing exports are not overwritten.

`Program.cs` registers the typed outgoing chat contract. `Commands.cs` consumes a bounded command
queue; file writes and command work do not block the packet callback. Ctrl+C cancels pending work
and disposes the host.

## MCP Room Tools: add your own MCP functions

```sh
dotnet run --project examples/McpRoomTools -- -p 9092
```

This starts a separate MCP server on port `9401` and prints its authenticated URL.
Keep that URL private. Configuration is stored under `QX Examples/McpRoomTools` in local application data.

| Tool | Arguments | Result |
| --- | --- | --- |
| `room_overview` | None | Room identity, readiness, rights and counts. |
| `find_furniture` | `query`, optional `limit` | Matches, their IDs, current positions and room context. |
| `walk_to_furniture` | `room_id`, `item_id` | Request movement after checking the room and item again. |

For example, call `find_furniture` with `{"query":"chair","limit":10}`. Pass the returned room ID
and an item ID to `walk_to_furniture`. Do not reuse IDs from a different room.

`RoomTools.cs` defines the schemas, read/write annotations and callbacks. It uses the same game
host as the native examples, without a script compiler or editor. This example exposes only its
own three tools. To expose the standard native tool set too, combine them with
<xref:Qx.Mcp.GameTools.Create(Qx.Game.Snapshots.GameQueries,Qx.Game.Api.IGameApi)>.

## Host lifetime

Create one <xref:Qx.Hosting.GameHost> per extension. Supply title, description, author and version
through <xref:Qx.Interception.GEarth.GEarthOptions>. Keep `GEarthOptions.Parse(args, ...)` so launches
from G-Earth retain their `-p`, `-f` and `-c` arguments.

`StartAsync` starts the host. It does not mean the hotel or room is ready. The examples check
readiness before using state, reconnect their transport, and dispose their subscriptions and host
when closed. The desktop example also moves UI work onto the UI thread.

| Entry point | Use |
| --- | --- |
| `host.Game` | State managers, room events and direct helpers such as `RoomActions.Walk`. |
| `host.Queries` | Detached snapshots with readiness, stale and truncation information. |
| `host.Api` | Discover and call typed game operations; subscribe to their events. |
| `host.Extension` | G-Earth lifecycle and packet interception. |
| `host.Messages` | Resolve named messages without fixed numeric headers. |

Extensions use separate game state and connections to the same G-Earth port. Immutable hotel data
and header catalogs share the QX cache. Give each extension its own settings directory. MCP servers
automatically choose an available port when the preferred port is occupied; use `server.ClientUrl`
to connect to the correct extension.

## Use packages instead of source references

The repository examples use project references so they run against the source you checked out.
For a separate project, install the published packages:

```sh
dotnet add package QX --version 0.1.2
dotnet add package QX.Mcp --version 0.1.2
```

Replace the QX project references with `QX`, and add `QX.Mcp` for the MCP example.
Keep every QX package on the same version. `QX.Mcp` is optional. `QX.Scripting` is only needed if
your application compiles and runs C# scripts. To test unpublished changes, build packages with
`./tools/pack-framework.ps1` and add `artifacts/framework` as a local NuGet source.

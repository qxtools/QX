# QX

C# framework for Habbo extensions. G-Earth connection, game state, typed actions, Wired tools and offline simulation.

[Documentation](https://qxscripter.xyz/api) [QX Scripter](https://github.com/QDaves/QX-Scripter)

## Start

Requires the .NET SDK from `global.json` and G-Earth.

```sh
git clone https://github.com/QDaves/QX.git
cd QX
dotnet run --project examples/RoomInspector -- -p 9092
```

The room inspector has a furniture list, search, item actions and JSON export. Enter a room after connecting.

Other examples: [local chat commands](examples/ChatCommands) and [custom MCP tools](examples/McpRoomTools).

## Packages

- `QX` connection, game state, native API and Wired.
- `QX.Core` low-level packets, models and interception; included by `QX`.
- `QX.Mcp` optional MCP server and game tools.
- `QX.Scripting` optional C# scripting.

```sh
dotnet add package QX --version 0.1.1
```

The remaining packages are pulled in as dependencies. The examples build directly from this repository.

## Build packages

```powershell
./tools/pack-framework.ps1
```

Output: `artifacts/framework`. Nothing is uploaded by this command.

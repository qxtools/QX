# MCP

`QX.Mcp` hosts a Streamable HTTP MCP server. It can expose the native game API, your own tools,
or both. It does not depend on QX Scripter or `QX.Scripting`.

```sh
dotnet add package QX.Mcp --version 0.1.0
```

## Native game tools

Create the server alongside your `GameHost`:

```csharp
using Qx.Mcp;

using var server = new McpServer(new McpServerOptions
{
    Name = "My Extension",
    Port = 9401,
    ConfigurationPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "My Extension", "mcp.json")
}, GameTools.Create(host.Queries, host.Api));

if (!server.Start())
    throw new InvalidOperationException(server.StartFailure);

Console.WriteLine(server.ClientUrl);
```

Keep the server alive for the host's lifetime. `ClientUrl` is the authenticated connection URL;
do not publish it. Choose a separate port and configuration file for each extension.
The configuration retains the generated access token between launches.

`GameTools.Create` supplies snapshot tools such as `get_room`, `get_furni` and `get_inventory`,
plus native operations named `application_*`. These tool names are part of the current MCP API.
They use the same typed requests and state checks as `host.Api`.

| Tool | Purpose |
| --- | --- |
| `list_application_members` | Discover game operations and their current availability. |
| `describe_application_member` | Inspect one operation's schema, requirements and C# call. |
| `get_connection` | Read connection, catalog and wire-profile readiness. |
| `get_room` | Read the current room and its completeness metadata. |
| `get_furni` | Read observed furniture, positions and states. |

Check `ready`, `loaded`, `stale`, `truncated` and `pending` in snapshot metadata before acting.
A successful dispatch does not prove that the hotel accepted an action.

## Your own tools

A <xref:Qx.Mcp.McpTool> defines its name, input schema, annotations and callback. Pass only the
tools your extension needs to the server constructor. Exposing a tool grants the connected client
that capability; read/write annotations describe it but do not enforce authorization.

The [MCP Room Tools example](https://github.com/QDaves/QX/tree/main/examples/McpRoomTools) implements
room inspection, furniture search and movement with checks for room changes and removed items.
See [Building an extension](extensions.md) for commands and file layout.

## Script and editor tools

Script files, editor tabs and the Wired knowledge bundle are features provided by QX Scripter.
They are not automatically added to a native framework MCP server.

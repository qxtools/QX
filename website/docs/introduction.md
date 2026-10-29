# Introduction

QX is a C# framework for Habbo extensions. It connects to G-Earth, tracks the current session
and provides typed game actions, requests, events and Wired tools.

## Native extensions

Create a <xref:Qx.Hosting.GameHost> and give it your extension's title, description, author and
version. Your application owns its window or console; QX owns the connection and game state.

| Entry point | Purpose |
| --- | --- |
| `host.Game` | Live state managers, native events and action helpers. |
| `host.Queries` | Detached snapshots with readiness and completeness metadata. |
| `host.Api` | Typed operations with consistent requests, results and availability. |
| `host.Extension` | G-Earth connection and packet interception. |
| `host.Messages` | Message names and layouts for the connected client. |

Use one host per extension. Separate processes have independent connections and room state;
downloaded hotel data and header catalogs can share the framework cache.

Start with [Getting started](getting-started.md) or the runnable [extension examples](extensions.md).

## Optional packages

`QX.Mcp` exposes game operations and custom tools over MCP. It does not require a script compiler,
editor or desktop UI. See [MCP](mcp.md).

`QX.Scripting` runs C# scripts against the same game state. Its globals make calls such as
`Walk(5, 8)` available without a prefix. The script guides describe this optional API; panel
rendering and editor behavior belong to the application hosting it.

[QX Scripter](https://github.com/QDaves/QX-Scripter) is one application built on the framework.
Your extension does not need to install or launch it.

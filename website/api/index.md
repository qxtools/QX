# API reference

This reference documents the public QX Framework API for native extensions, MCP hosts and scripts.

Start a native extension with <xref:Qx.Hosting.GameHost>. Use <xref:Qx.Game.Api.GameApi>
for game operations and <xref:Qx.Game.Snapshots.GameQueries> for snapshots. The optional
`QX.Scripting` package adds C# scripting; `QX.Mcp` exposes native functions through MCP.

Install the `QX` NuGet package for a native extension. It includes the hosting, game, client-data
and Wired assemblies and depends on `QX.Core`. Assembly and namespace names below do not require
separate NuGet installations. See [Getting started](../docs/getting-started.md) for working code.

Scripts run with the members of <xref:Qx.Scripting.ScriptGlobals> in scope, so `Room`, `Users`,
`Talk` and `OnIn` are written without a prefix. The other types are what those members return or
accept, and the host that compiles and runs scripts.

A script imports every namespace marked imported. A type from a namespace marked add using needs a
`using` directive or its full name, and the host namespaces make up the app around the scripts.

| Namespace | Contents | In scripts |
| --- | --- | --- |
| <xref:Qx.Hosting> | Extension startup, shutdown, configuration and shared cache paths. | host |
| <xref:Qx.Mcp> | MCP servers, tool definitions and native game tools. | host |
| <xref:Qx.Scripting> | The script globals, the panel and keyboard APIs, queries, room scopes and the helper types of the globals. | imported |
| <xref:Qx.Game> | The live game state and shared state-manager lifecycle. | imported |
| <xref:Qx.Game.Rooms> | Rooms, avatars, furniture, movement, chat and moderation. | imported |
| <xref:Qx.Game.Inventory> | Inventory state, refreshes and item queries. | imported |
| <xref:Qx.Game.Catalog> | Catalog pages, offers, purchases and cache. | imported |
| <xref:Qx.Game.Marketplace> | Marketplace state, searches and transactions. | imported |
| <xref:Qx.Game.Profiles> / <xref:Qx.Game.Friends> | Profiles, relationships and friends. | imported |
| <xref:Qx.Game.Achievements> / <xref:Qx.Game.Tasks> | Badges, achievements, quests, daily tasks and Habbicons. | imported |
| <xref:Qx.Game.Wallet> / <xref:Qx.Game.Subscriptions> | Balances, earnings and subscriptions. | imported |
| <xref:Qx.Game.Trading> / <xref:Qx.Game.Gifts> / <xref:Qx.Game.Crafting> | Trading, gifts and crafting. | imported |
| <xref:Qx.Game.Forums> / <xref:Qx.Game.Leaderboards> / <xref:Qx.Game.Polls> / <xref:Qx.Game.Navigator> | Forums, leaderboards, polls and room searches. | imported |
| <xref:Qx.Game.Data> | Downloaded hotel data, furniture definitions and external texts. | imported |
| <xref:Qx.Game.Wired> | Wired state, configuration, inspection and building. | imported |
| <xref:Qx.Model> | Avatars, furni, rooms, users, groups, forums, the marketplace, polls, quests and the other game models. | imported |
| <xref:Qx.Model.Figures> | Avatar figures, their parts and colors, and figure validation. | imported |
| <xref:Qx.Model.Wired> | Wired boxes, variables, chests and their messages. | imported |
| <xref:Qx.Model.Messages.Incoming> | Parsed messages the hotel sends. | imported |
| <xref:Qx.Model.Messages.Outgoing> | Messages the client sends. | imported |
| <xref:Qx.Game.Api> | The game API behind `Api`, MCP and the command line: operation ids, input types, results, events and readiness. | imported |
| <xref:Qx.Game.Wired.Simulation> | Offline Wired simulation, detached room models, events, diagnostics and assertions. | imported |
| <xref:Qx.Interception> | Packet interception: the interceptor, intercepted packets and the hotel session. | imported |
| <xref:Qx.Protocol> | Packets, headers, binary readers and writers, message keys, `Msg`, catalogs and wire profiles. | imported |
| <xref:Qx.Platform> | The operating system, keys and the raw keyboard reader. | imported |
| <xref:Qx> | Ids, lengths, message directions, the storage paths and the product version. | imported |
| <xref:Qx.Game.Messages> | Message contracts, request coordination and request errors. | imported |
| <xref:Qx.Game.Snapshots> | The JSON read model the MCP read tools return. | add using |
| <xref:Qx.Scripting.Hosting> | Compiling and running scripts, panel layouts and the API catalog. | host |
| <xref:Qx.Interception.GEarth> | G-Earth connections, extension metadata and launch arguments. | host |
| <xref:Qx.Diagnostics> | The diagnostic log. | host |

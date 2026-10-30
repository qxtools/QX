# Command line

A QX extension can be a console application. It uses the same `GameHost`, state managers and
native API as a desktop extension; no window or script compiler is required.

The [Chat Commands example](https://github.com/qxscripter/QX/tree/main/examples/ChatCommands) is a complete
console extension with furniture search, local chat commands and JSON export.

```sh
dotnet run --project examples/ChatCommands -- -p 9092
```

## G-Earth arguments

Pass the application's arguments to `GEarthOptions.Parse(args, defaults)`. This preserves the port
and launch arguments supplied by G-Earth. Set title, description, author and version in the defaults.

## Lifetime

Start the host with `StartAsync`, then await `TransportTask` to keep the process alive. Use a
cancellation token for Ctrl+C and dispose the host when the application exits. With
`ReconnectTransport = true`, the host reconnects its transport until cancelled or disposed.

Keep packet callbacks short. The example queues local commands so exports and other command work
do not block incoming packets. Check room readiness before using room state.

## Other applications

[QX Scripter](https://github.com/qxscripter/QX-Scripter) supplies its own script shell and command-line
interface. Those commands belong to that application, not to the framework packages.

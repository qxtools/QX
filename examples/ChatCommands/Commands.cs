using System.Globalization;
using System.Text.Json;
using System.Threading.Channels;
using Qx;
using Qx.Game.Snapshots;
using Qx.Hosting;

namespace ChatCommands;

public sealed class Commands : IAsyncDisposable
{
    readonly GameHost host;
    readonly Action<string> output;
    readonly CancellationTokenSource stop = new();
    readonly Channel<string> queue = Channel.CreateBounded<string>(new BoundedChannelOptions(16)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.Wait
    });
    readonly Task worker;

    public Commands(GameHost host, Action<string> output)
    {
        this.host = host;
        this.output = output;
        worker = ReadCommands();
    }

    public static bool IsCommand(string text) =>
        text.Equals(".qx", StringComparison.OrdinalIgnoreCase) ||
        text.StartsWith(".qx ", StringComparison.OrdinalIgnoreCase);

    public bool Enqueue(string text) => IsCommand(text) && queue.Writer.TryWrite(text[3..].Trim());

    async Task ReadCommands()
    {
        try
        {
            await foreach (string line in queue.Reader.ReadAllAsync(stop.Token))
            {
                try { await Run(line); }
                catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
                catch (Exception error) { output(error.Message); }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    }

    async Task Run(string line)
    {
        string[] parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        string command = parts.Length == 0 ? "help" : parts[0].ToLowerInvariant();
        string argument = parts.Length < 2 ? "" : parts[1].Trim();
        switch (command)
        {
            case "help":
                output(".qx room | .qx find <name/class/id> | .qx go <x> <y> | .qx use <id> | .qx export");
                break;
            case "room":
                var room = host.Queries.Room();
                if (room.Error is not null || !room.Metadata.Ready || room.Metadata.Stale)
                    throw new InvalidOperationException("Wait until the room is ready.");
                output($"Room {room.Data?.Id}: {room.Data?.AvatarCount} avatars, {room.Data?.FloorItemCount} floor items, {room.Data?.WallItemCount} wall items.");
                break;
            case "find":
                if (argument.Length == 0) throw new ArgumentException("Usage: .qx find <name/class/id>");
                var snapshot = Furniture();
                var matches = snapshot.Data!.FloorItems.Where(item => !item.IsRemoved && (
                    item.Id.ToString().Contains(argument, StringComparison.OrdinalIgnoreCase) ||
                    (item.Identifier ?? "").Contains(argument, StringComparison.OrdinalIgnoreCase) ||
                    (item.Definition?.Name ?? "").Contains(argument, StringComparison.OrdinalIgnoreCase))).ToArray();
                foreach (var item in matches.Take(20))
                    output($"{item.Id}: {item.Definition?.Name ?? item.Identifier ?? "Unknown"} ({item.Position.X}, {item.Position.Y}, {item.Position.Z}) state={item.State}");
                output($"{matches.Length} matches in observed furniture." + (snapshot.Metadata.Loaded ? "" : " Some data is still loading."));
                break;
            case "go":
                string[] coordinates = argument.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (coordinates.Length != 2 || !int.TryParse(coordinates[0], out int x) || !int.TryParse(coordinates[1], out int y) || x < 0 || y < 0)
                    throw new ArgumentException("Usage: .qx go <x> <y>");
                host.Game.RoomActions.Walk(x, y);
                output("Walk requested; the server decides whether the tile is reachable.");
                break;
            case "use":
                if (!Id.TryParse(argument, out Id id)) throw new ArgumentException("Usage: .qx use <id>");
                host.Game.Room.Capture(room_state =>
                {
                    if (!room_state.IsReady || !room_state.FloorItems.Any(item => item.Id == id))
                        throw new InvalidOperationException("That floor item is not in the current room.");
                    host.Game.RoomActions.UseFloorItem(id);
                    return true;
                });
                output($"Use request sent for {id}.");
                break;
            case "export":
                var current = Furniture();
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QX Examples", "ChatCommands", "Exports");
                Directory.CreateDirectory(directory);
                string name = $"room-{current.Data!.RoomId}-{DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff", CultureInfo.InvariantCulture)}.json";
                string path = Path.Combine(directory, name);
                await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true))
                    await JsonSerializer.SerializeAsync(file, current, new JsonSerializerOptions { WriteIndented = true }, stop.Token);
                output(path);
                break;
            default:
                output("Unknown command. Type .qx help.");
                break;
        }
    }

    QueryEnvelope<FurniCollectionSnapshot> Furniture()
    {
        var snapshot = host.Queries.Furni();
        if (snapshot.Error is not null || snapshot.Data is null || !snapshot.Metadata.Ready || snapshot.Metadata.Stale)
            throw new InvalidOperationException("Wait until the room is ready.");
        return snapshot;
    }

    public async ValueTask DisposeAsync()
    {
        queue.Writer.TryComplete();
        stop.Cancel();
        await worker;
        stop.Dispose();
    }
}

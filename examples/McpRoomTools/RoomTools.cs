using Qx.Game.Api;
using Qx.Game.Rooms;
using Qx.Hosting;
using Qx.Mcp;

namespace McpRoomTools;

public static class RoomTools
{
    public static IReadOnlyList<McpTool> Create(GameHost host) =>
    [
        new()
        {
            Name = "room_overview",
            Description = "Read the current room, its load state, authority and furniture counts.",
            InputSchema = McpSchema.Schema(),
            Annotations = new(true, false, true, false),
            Handler = (_, cancellation) =>
            {
                cancellation.ThrowIfCancellationRequested();
                return Task.FromResult(GameJson.Serialize(host.Queries.Room()));
            }
        },
        new()
        {
            Name = "find_furniture",
            Description = "Search observed floor furniture by name, class name or ID. Includes current room ID, generation and completeness metadata.",
            InputSchema = McpSchema.MixedSchema([("query", "string", "Search text; empty returns all observed furniture.")],
                ("limit", "integer", "Maximum results.", 20, 1, 100)),
            Annotations = new(true, false, true, false),
            Handler = (arguments, cancellation) =>
            {
                cancellation.ThrowIfCancellationRequested();
                string query = McpArguments.Str(arguments, "query").Trim();
                int limit = McpArguments.BoundedInt(arguments, "limit", 20, 1, 100);
                var snapshot = host.Queries.Furni();
                if (snapshot.Error is not null || snapshot.Data is null || !snapshot.Metadata.Ready || snapshot.Metadata.Stale)
                    throw new McpToolException("The room is not ready. Read room_overview before retrying.");
                var found = snapshot.Data.FloorItems.Where(item => !item.IsRemoved && (
                    item.Id.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (item.Identifier ?? "").Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (item.Definition?.Name ?? "").Contains(query, StringComparison.OrdinalIgnoreCase))).OrderBy(item => item.Id).ToArray();
                return Task.FromResult(GameJson.Serialize(new
                {
                    RoomId = snapshot.Data.RoomId,
                    snapshot.Data.Generation,
                    snapshot.Metadata,
                    Matched = found.Length,
                    Returned = Math.Min(found.Length, limit),
                    Items = found.Take(limit).Select(item => new
                    {
                        ItemId = item.Id,
                        Name = item.Definition?.Name,
                        item.Identifier,
                        item.Position,
                        item.State,
                        item.OwnerName
                    })
                }));
            }
        },
        new()
        {
            Name = "walk_to_furniture",
            Description = "Request movement to the current tile of one floor item. Rejects a stale room ID or missing item. The server may reject an unreachable tile.",
            InputSchema = McpSchema.Schema(("room_id", "id", "Room ID returned by find_furniture."),
                ("item_id", "id", "Floor item ID returned by find_furniture.")),
            Annotations = new(false, false, true, true),
            Handler = (arguments, cancellation) =>
            {
                cancellation.ThrowIfCancellationRequested();
                long room_id = McpArguments.Long(arguments, "room_id");
                long item_id = McpArguments.Long(arguments, "item_id");
                var result = host.Game.Room.Capture(room =>
                {
                    if (!room.IsReady || room.RoomId != room_id)
                        throw new McpToolException("The room changed or is not ready. Search again in the current room.");
                    var item = room.FloorItems.FirstOrDefault(item => item.Id == item_id);
                    if (item is null) throw new McpToolException("That floor item is no longer in the room.");
                    return host.Api.Invoke<RoomAvatarWalkRequest, RoomAvatarDispatchResult>(
                        ApiIds.RoomAvatarWalk, new RoomAvatarWalkRequest(item.X, item.Y), cancellation);
                });
                return Task.FromResult(GameJson.Serialize(result));
            }
        }
    ];
}

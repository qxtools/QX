using Qx.Game.Api;
using Qx.Game.Snapshots;

namespace Qx.Mcp;

public static class GameTools
{
    public static IReadOnlyList<McpTool> Create(GameQueries queries, IGameApi application)
    {
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(application);
        return Array.AsReadOnly<McpTool>(
        [
            Read("get_connection", "Read connection, catalog and wire-profile readiness.", queries.Connection),
            Read("get_room", "Read the current room, authority, environment and content readiness.", queries.Room),
            Read("get_avatars", "Read users, pets and bots observed in the current room.", queries.Avatars),
            Read("get_furni", "Read observed room furniture with identifiers, state and position.", queries.Furni),
            Read("get_profile", "Read the observed local account profile.", queries.Profile),
            Read("get_friends", "Read the observed friend list and its loading state.", queries.Friends),
            Read("get_inventory", "Read observed inventory items and their loading state.", queries.Inventory),
            Read("get_controllers", "Read the current room's controllers.", queries.Controllers),
            Read("get_currencies", "Read observed currency balances.", queries.Currencies),
            Read("get_heightmap", "Read the observed room heightmap and its loading state.", queries.Heightmap),
            .. GameApiTools.Create(application)
        ]);
    }

    private static McpTool Read<T>(string name, string description, Func<QueryEnvelope<T>> read) => new()
    {
        Name = name,
        Description = description + " This reads local state only. Check ready, loaded, stale, truncated and pending metadata before acting; collection limits come from the query service.",
        InputSchema = McpSchema.Schema(),
        OutputSchema = GameJson.OutputSchema(typeof(QueryEnvelope<T>)),
        Annotations = new McpToolAnnotations(true, false, true, false),
        Handler = (_, cancellation) =>
        {
            cancellation.ThrowIfCancellationRequested();
            return Task.FromResult(GameJson.Serialize(read()));
        }
    };
}

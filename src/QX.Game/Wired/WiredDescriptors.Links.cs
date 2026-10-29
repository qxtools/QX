using Qx.Game.Api;
using Qx.Model.Wired;
using Qx.Protocol;

namespace Qx.Game.Wired;

internal static partial class WiredDescriptors
{
    private const string link_scope =
        " Networks belong to the account; only the Link's furniture owner can configure it. The Link must be in the current room.";

    public static ApiDescriptor LinkConfigurationGet { get; } = RequestResponse<WiredLinkRequest, WiredLinkConfiguration>(
        ApiIds.WiredLinkConfigurationGet,
        "Read WIRED Link configuration",
        "Reads a WIRED Link's connected network, own-room receiving flag and every network of the account." + link_scope +
        " The selected network is \"0\" while disconnected. The Link window stays closed unless hide_window is false.",
        MessageKeys.Wired.Link.ConfigurationGet,
        MessageKeys.Wired.Link.Configuration,
        [LinkParameter(), HideParameter(), TimeoutParameter()],
        ReadHints());

    public static ApiDescriptor LinkConfigurationSave { get; } = RequestResponse<WiredLinkSaveRequest, WiredLinkOperationResult>(
        ApiIds.WiredLinkConfigurationSave,
        "Save WIRED Link configuration",
        "Connects a WIRED Link to a network, or disconnects it with network_id \"0\", and sets whether it receives messages and dataframes sent from its own room." + link_scope +
        " Only one Link per room may use a network (result NetworkUsedInRoom). Without own-room receiving, a connected Link receives only from other rooms; delivery reaches loaded rooms only.",
        MessageKeys.Wired.Link.ConfigurationSave,
        MessageKeys.Wired.Link.Result,
        [LinkParameter(),
         new("network_id", typeof(string), true, null, "Network identifier from the configuration, or \"0\" to disconnect.", TextConstraint()),
         new("receive_local", typeof(bool), true, null, "True to receive messages and dataframes sent from this room."),
         HideParameter(), TimeoutParameter()],
        WriteHints(false, true));

    public static ApiDescriptor LinkNetworkCreate { get; } = RequestResponse<WiredLinkNetworkCreateRequest, WiredLinkOperationResult>(
        ApiIds.WiredLinkNetworkCreate,
        "Create WIRED Link network",
        "Creates a network owned by the account. It does not connect the Link; save the configuration afterwards. Names are exact and case-sensitive; duplicates, blank names and names over 255 characters are rejected, and accounts have a network limit." + link_scope,
        MessageKeys.Wired.Link.NetworkCreate,
        MessageKeys.Wired.Link.Result,
        [LinkParameter(),
         new("name", typeof(string), true, null, "Exact network name, 1 to 255 characters with visible text.", TextConstraint()),
         HideParameter(), TimeoutParameter()],
        WriteHints(false, false));

    public static ApiDescriptor LinkNetworkRename { get; } = RequestResponse<WiredLinkNetworkRenameRequest, WiredLinkOperationResult>(
        ApiIds.WiredLinkNetworkRename,
        "Rename WIRED Link network",
        "Renames a network owned by the account. Existing connections are unchanged." + link_scope,
        MessageKeys.Wired.Link.NetworkRename,
        MessageKeys.Wired.Link.Result,
        [LinkParameter(),
         new("network_id", typeof(string), true, null, "Network identifier.", TextConstraint()),
         new("name", typeof(string), true, null, "New exact network name, 1 to 255 characters with visible text.", TextConstraint()),
         HideParameter(), TimeoutParameter()],
        WriteHints(false, true));

    public static ApiDescriptor LinkNetworkDelete { get; } = RequestResponse<WiredLinkNetworkRequest, WiredLinkOperationResult>(
        ApiIds.WiredLinkNetworkDelete,
        "Delete WIRED Link network",
        "Deletes a network owned by the account and disconnects all of its Links in every room. This cannot be undone." + link_scope,
        MessageKeys.Wired.Link.NetworkDelete,
        MessageKeys.Wired.Link.Result,
        [LinkParameter(),
         new("network_id", typeof(string), true, null, "Network identifier.", TextConstraint()),
         HideParameter(), TimeoutParameter()],
        WriteHints(true, false));

    public static ApiDescriptor LinkConnectionsGet { get; } = RequestResponse<WiredLinkNetworkRequest, WiredLinkConnections>(
        ApiIds.WiredLinkConnectionsGet,
        "List WIRED Link connections",
        "Lists every Link connected to a network, including saved connections in unloaded rooms, with room, Builders Club and own-room receiving flags." + link_scope,
        MessageKeys.Wired.Link.ConnectionsGet,
        MessageKeys.Wired.Link.Connections,
        [LinkParameter(),
         new("network_id", typeof(string), true, null, "Network identifier.", TextConstraint()),
         HideParameter(), TimeoutParameter()],
        ReadHints());

    private static ApiParameter LinkParameter() =>
        new("link_id", typeof(Id), true, null, "WIRED Link (wf_link) furniture identifier in the current room.", IdConstraint());

    private static ApiParameter HideParameter() =>
        new("hide_window", typeof(bool), false, true, "Keep the client's WIRED Link window from opening or updating.");
}

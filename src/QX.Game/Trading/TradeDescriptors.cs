using System.Diagnostics.CodeAnalysis;
using Qx.Game.Api;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Game.Trading;

internal static class TradeDescriptors
{
    private static readonly ApiExposure event_exposure =
        ApiExposure.Ui | ApiExposure.Cli | ApiExposure.Scripting;

    public static ApiDescriptor State { get; } = new(
        ApiIds.TradeState,
        "Trade state",
        "Reads a bounded immutable projection of the active trade and NFT inventory summary.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(TradeStateRequest),
        typeof(TradeStateView),
        [OutputLimit("offer_item_limit", "Maximum items returned for each participant offer."), OutputLimit("nft_offer_limit", "Maximum NFT assets returned for each participant offer.")],
        stateEffects:
        [
            new(GameRequirement.RoomActive, ApiStateChange.Reads),
            new(GameRequirement.TradeActive, ApiStateChange.Reads),
            new(GameRequirement.TradeNftInventoryLoaded, ApiStateChange.Reads)
        ],
        messages: StateMessages(),
        toolHints: new(true, false, true, false),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor Open { get; } = Dispatch<TradeOpenRequest>(
        ApiIds.TradeOpen,
        "Open trade",
        "Requests a trade with one user in the current ready room.",
        [
            new("user_index", typeof(int), true, null, "Target avatar room index.", new(Minimum: 0)),
            .. GuardParameters(),
            new("expected_room_generation", typeof(long?), false, null, "Optional ready-room generation guard for this transient avatar index.", new(Minimum: 0)),
            new("expected_user_id", typeof(Id?), false, null, "Optional target identity guard for this transient avatar index.", new(Pattern: "^[1-9][0-9]*$"))
        ],
        MessageKeys.Trade.OpenRequest,
        [GameRequirement.TradeInactive]);

    public static ApiDescriptor ItemsAdd { get; } = Dispatch<TradeItemsAddRequest>(
        ApiIds.TradeItemsAdd,
        "Add trade items",
        "Adds distinct inventory item identifiers to the active trade offer.",
        [new("item_ids", typeof(IReadOnlyList<Id>), true, null, "Distinct nonzero inventory item identifiers valid for Flash.", new(MinItems: 1, MaxItems: ushort.MaxValue)), .. GuardParameters()],
        MessageKeys.Trade.ItemsAdd,
        [GameRequirement.TradeTrading]);

    public static ApiDescriptor ItemRemove { get; } = Dispatch<TradeItemRemoveRequest>(
        ApiIds.TradeItemRemove,
        "Remove trade item",
        "Removes one inventory item identifier from the active trade offer.",
        [new("item_id", typeof(Id), true, null, "Nonzero inventory item identifier valid for Flash.", new(Pattern: "^-?[1-9][0-9]*$")), .. GuardParameters()],
        MessageKeys.Trade.ItemRemove,
        [GameRequirement.TradeTrading]);

    public static ApiDescriptor Accept { get; } = Command(
        ApiIds.TradeAccept,
        "Accept trade",
        "Accepts the current offer state without assuming hotel confirmation.",
        MessageKeys.Trade.Accept,
        [
            GameRequirement.TradeTrading,
            GameRequirement.ProfileLoaded,
            GameRequirement.TradeLocalCanTrade,
            GameRequirement.TradeSilverFeeReached
        ]);

    public static ApiDescriptor Unaccept { get; } = Command(
        ApiIds.TradeUnaccept,
        "Withdraw trade acceptance",
        "Withdraws acceptance from the active trade.",
        MessageKeys.Trade.Unaccept,
        [GameRequirement.TradeActive]);

    public static ApiDescriptor Confirm { get; } = Command(
        ApiIds.TradeConfirm,
        "Confirm trade",
        "Dispatches final confirmation for a trade awaiting confirmation.",
        MessageKeys.Trade.Confirm,
        [
            GameRequirement.TradeAwaitingConfirmation,
            GameRequirement.ProfileLoaded,
            GameRequirement.TradeLocalCanTrade,
            GameRequirement.TradeSilverFeeReached
        ]);

    public static ApiDescriptor Close { get; } = Command(
        ApiIds.TradeClose,
        "Close trade",
        "Requests closure of the active trade without assuming the hotel accepted it.",
        MessageKeys.Trade.Close,
        [GameRequirement.TradeActive]);

    public static ApiDescriptor NftInventoryList { get; } = new(
        ApiIds.TradeNftInventoryList,
        "Trade NFT inventory page",
        "Reads one bounded page from an immutable session-bound NFT inventory snapshot.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(TradeNftInventoryPageRequest),
        typeof(TradeNftInventoryPage),
        PageParameters(),
        stateEffects:
        [new(GameRequirement.TradeNftInventoryLoaded, ApiStateChange.Reads)],
        messages: [Observe(MessageKeys.Trade.NftInventory, false)],
        toolHints: new(true, false, true, false),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor NftInventoryRefresh { get; } = new(
        ApiIds.TradeNftInventoryRefresh,
        "Refresh trade NFT inventory",
        "Loads the Flash trade NFT inventory and returns its first bounded snapshot page.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(TradeNftInventoryRefreshRequest),
        typeof(TradeNftInventoryPage),
        [Limit(), Timeout()],
        [GameRequirement.HotelConnected],
        [new(GameRequirement.TradeNftInventoryLoaded, ApiStateChange.Changes)],
        [
            Send(MessageKeys.Trade.NftInventoryRequest),
            Observe(MessageKeys.Trade.NftInventory)
        ],
        new(true, false, true, true));

    public static ApiDescriptor Changed { get; } = new(
        ApiIds.TradeChanged,
        "Trade changed",
        "Publishes bounded lifecycle summaries without embedding offer or NFT inventory lists.",
        ApiMemberKind.Event,
        event_exposure,
        null,
        typeof(TradeChanged),
        stateEffects:
        [
            new(GameRequirement.TradeActive, ApiStateChange.Changes),
            new(GameRequirement.TradeNftInventoryLoaded, ApiStateChange.Changes)
        ],
        messages: StateMessages());

    private static ApiDescriptor Command(
        string id,
        string title,
        string description,
        MessageKey key,
        IReadOnlyList<GameRequirement> required_states) => Dispatch<TradeCommandRequest>(
            id,
            title,
            description,
            GuardParameters(),
            key,
            required_states);

    private static ApiDescriptor Dispatch<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRequest>(
        string id,
        string title,
        string description,
        IReadOnlyList<ApiParameter> parameters,
        MessageKey key,
        IReadOnlyList<GameRequirement> required_states) => new(
            id,
            title,
            description,
            ApiMemberKind.Operation,
            ApiExposure.All,
            typeof(TRequest),
            typeof(TradeDispatchResult),
            parameters,
            [GameRequirement.HotelConnected, GameRequirement.RoomReady, .. required_states],
            [new(GameRequirement.TradeActive, ApiStateChange.Changes)],
            [Send(key)],
            new(false, true, false, true));

    private static IReadOnlyList<ApiMessageRequirement> StateMessages() =>
    [
        Observe(MessageKeys.Trade.Opened),
        Observe(MessageKeys.Trade.Offers),
        Observe(MessageKeys.Trade.AcceptanceUpdated),
        Observe(MessageKeys.Trade.Confirmation),
        Observe(MessageKeys.Trade.Completed),
        Observe(MessageKeys.Trade.Closed),
        Observe(MessageKeys.Trade.OpenFailed),
        Observe(MessageKeys.Trade.NftOffers, false),
        Observe(MessageKeys.Trade.NftInventory, false),
        Observe(MessageKeys.Trade.SilverUpdated, false),
        Observe(MessageKeys.Trade.SilverFee, false)
    ];

    private static IReadOnlyList<ApiParameter> PageParameters() =>
    [
        new("offset", typeof(int), false, 0, "Zero-based offset within the immutable snapshot.", new(Minimum: 0)),
        Limit(),
        new("snapshot_revision", typeof(long?), false, null, "Snapshot revision returned by the first page and required for continuation pages.", new(Minimum: 1))
    ];

    private static IReadOnlyList<ApiParameter> GuardParameters() =>
    [
        new("expected_session_generation", typeof(long?), false, null, "Optional session generation guard from trade.state or trade.changed.", new(Minimum: 0)),
        new("expected_revision", typeof(long?), false, null, "Optional state revision guard from trade.state or trade.changed.", new(Minimum: 0)),
        new("expected_epoch", typeof(long?), false, null, "Optional trade epoch guard from trade.state or trade.changed.", new(Minimum: 0))
    ];

    private static ApiParameter OutputLimit(string name, string description) =>
        new(name, typeof(int), false, 100, description, new(Minimum: 0, Maximum: 500));

    private static ApiParameter Limit() => new(
        "limit",
        typeof(int),
        false,
        100,
        "Maximum NFT assets returned by this page.",
        new(Minimum: 1, Maximum: 500));

    private static ApiParameter Timeout() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum time to wait for the correlated hotel response.",
        new(Minimum: 1, Maximum: 120000));

    private static ApiMessageRequirement Send(MessageKey key) =>
        new(key, MessageDirection.Out, ApiMessageRole.Send);

    private static ApiMessageRequirement Observe(MessageKey key, bool required = true) =>
        new(key, MessageDirection.In, ApiMessageRole.Observe, required);
}

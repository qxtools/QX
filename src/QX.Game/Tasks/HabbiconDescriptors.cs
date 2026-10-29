using System.Diagnostics.CodeAnalysis;
using Qx.Game.Api;
using Qx.Protocol;

namespace Qx.Game.Tasks;

internal static class HabbiconDescriptors
{
    private static readonly ApiExposure event_exposure =
        ApiExposure.Ui | ApiExposure.Cli | ApiExposure.Scripting;

    public static ApiDescriptor State { get; } = Query(
        ApiIds.HabbiconsState,
        "Habbicon state",
        "Reads the immutable habbicon vault state.",
        typeof(HabbiconStateRequest),
        typeof(HabbiconStateView),
        [SnapshotRevisionParameter(false)]);

    public static ApiDescriptor Collections { get; } = Query(
        ApiIds.HabbiconCollectionsList,
        "Habbicon collections",
        "Reads one hotel-ordered collection page from an immutable snapshot.",
        typeof(HabbiconCollectionPageRequest),
        typeof(HabbiconCollectionPage),
        PageParameters());

    public static ApiDescriptor Entries { get; } = Query(
        ApiIds.HabbiconEntriesList,
        "Habbicon entries",
        "Reads one hotel-ordered icon page from an immutable snapshot.",
        typeof(HabbiconEntryPageRequest),
        typeof(HabbiconEntryPage),
        PageParameters());

    public static ApiDescriptor ShopRefresh { get; } = new(
        ApiIds.HabbiconShopRefresh,
        "Refresh habbicon shop",
        "Reloads the habbicon shop from the server. Returns the first fresh shop snapshot after one uniquely correlated request.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(HabbiconShopRefreshRequest),
        typeof(HabbiconShopRefreshResult),
        RefreshParameters(),
        [GameRequirement.HotelConnected],
        [ChangeEffect()],
        [Send(MessageKeys.Habbicons.ShopRequest), Observe(MessageKeys.Habbicons.ShopSnapshot)],
        new ApiToolHints(true, false, true, true));

    public static ApiDescriptor InfoRefresh { get; } = new(
        ApiIds.HabbiconInfoRefresh,
        "Refresh habbicon info",
        "Returns fresh detail for the requested icon; the response echoes the icon identifier.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(HabbiconInfoRefreshRequest),
        typeof(HabbiconInfoRefreshResult),
        [
            IdParameter("habbicon_id", "Habbicon identifier."),
            TimeoutParameter(),
            ExpectedSessionParameter()
        ],
        [GameRequirement.HotelConnected],
        [ChangeEffect()],
        [Send(MessageKeys.Habbicons.InfoRequest), Observe(MessageKeys.Habbicons.InfoSnapshot)],
        new ApiToolHints(true, false, true, true));

    public static ApiDescriptor Buy { get; } = Action<HabbiconBuyActionRequest>(
        ApiIds.HabbiconBuy,
        "Buy habbicon",
        "Buys one habbicon.",
        MessageKeys.Habbicons.Buy,
        IdParameter("habbicon_id", "Habbicon identifier."));

    public static ApiDescriptor BuyCollection { get; } =
        Action<HabbiconCollectionBuyActionRequest>(
            ApiIds.HabbiconCollectionBuy,
            "Buy habbicon collection",
            "Buys one habbicon collection.",
            MessageKeys.Habbicons.BuyCollection,
            IdParameter("collection_id", "Collection identifier."));

    public static ApiDescriptor Claim { get; } = Action<HabbiconClaimActionRequest>(
        ApiIds.HabbiconClaim,
        "Claim habbicon",
        "Claims one earned habbicon.",
        MessageKeys.Habbicons.Claim,
        IdParameter("habbicon_id", "Habbicon identifier."));

    public static ApiDescriptor Favorite { get; } = Action<HabbiconFavoriteActionRequest>(
        ApiIds.HabbiconFavorite,
        "Favorite habbicon",
        "Marks one habbicon as favorite.",
        MessageKeys.Habbicons.Favorite,
        IdParameter("habbicon_id", "Habbicon identifier."));

    public static ApiDescriptor Unfavorite { get; } = Action<HabbiconUnfavoriteActionRequest>(
        ApiIds.HabbiconUnfavorite,
        "Unfavorite habbicon",
        "Removes one habbicon from favorites.",
        MessageKeys.Habbicons.Unfavorite,
        IdParameter("habbicon_id", "Habbicon identifier."));

    public static ApiDescriptor Changed { get; } = new(
        ApiIds.HabbiconsChanged,
        "Habbicons changed",
        "Publishes bounded shop, inventory, status, info, room-use, setting, and reset transitions.",
        ApiMemberKind.Event,
        event_exposure,
        null,
        typeof(HabbiconChanged),
        stateEffects: [ChangeEffect()],
        messages: ObservedMessages(),
        invocationScope: ApiLifetime.Persistent);

    private static ApiDescriptor Query(
        string id,
        string title,
        string description,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type request,
        Type result,
        IReadOnlyList<ApiParameter> parameters) =>
        new(
            id,
            title,
            description,
            ApiMemberKind.Query,
            ApiExposure.All,
            request,
            result,
            parameters,
            stateEffects: [ReadEffect()],
            messages: ObservedMessages(),
            toolHints: new ApiToolHints(true, false, true, false),
            invocationScope: ApiLifetime.Persistent);

    private static ApiDescriptor Action<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(
        string id,
        string title,
        string description,
        MessageKey key,
        ApiParameter parameter) =>
        new(
            id,
            title,
            description,
            ApiMemberKind.Operation,
            ApiExposure.All,
            typeof(T),
            typeof(HabbiconDispatchResult),
            [parameter, ExpectedSessionParameter()],
            [GameRequirement.HotelConnected],
            [ChangeEffect()],
            [Send(key)],
            new ApiToolHints(false, true, false, true));

    private static IReadOnlyList<ApiParameter> PageParameters() =>
    [
        new("offset", typeof(int), false, 0, "Zero-based snapshot offset.", new(Minimum: 0)),
        new("limit", typeof(int), false, 100, "Maximum values returned.", new(Minimum: 1, Maximum: 500)),
        SnapshotRevisionParameter(true)
    ];

    private static IReadOnlyList<ApiParameter> RefreshParameters() =>
    [
        new("limit", typeof(int), false, 100, "Maximum first-page values returned.", new(Minimum: 1, Maximum: 500)),
        TimeoutParameter(),
        ExpectedSessionParameter()
    ];

    private static ApiParameter SnapshotRevisionParameter(bool continuation) =>
        new(
            "snapshot_revision",
            typeof(long?),
            false,
            null,
            continuation
                ? "Snapshot revision returned by the first read and required for continuation pages."
                : "Optional retained snapshot revision; omitted to capture current state.",
            new(Minimum: 1));

    private static ApiParameter TimeoutParameter() =>
        new(
            "timeout_milliseconds",
            typeof(int),
            false,
            10000,
            "Total response budget.",
            new(Minimum: 1, Maximum: 120000));

    private static ApiParameter ExpectedSessionParameter() =>
        new(
            "expected_session_generation",
            typeof(long?),
            false,
            null,
            "Optional active session generation required through dispatch.",
            new(Minimum: 1));

    private static ApiParameter IdParameter(string name, string description) =>
        new(name, typeof(int), true, null, description);

    private static IReadOnlyList<ApiMessageRequirement> ObservedMessages() =>
    [
        Observe(MessageKeys.Habbicons.ShopSnapshot),
        Observe(MessageKeys.Habbicons.InventorySnapshot),
        Observe(MessageKeys.Habbicons.StatusUpdated),
        Observe(MessageKeys.Habbicons.InfoSnapshot),
        Observe(MessageKeys.Habbicons.RoomUsed)
    ];

    private static ApiMessageRequirement Send(MessageKey key) =>
        new(key, MessageDirection.Out, ApiMessageRole.Send);

    private static ApiMessageRequirement Observe(MessageKey key) =>
        new(key, MessageDirection.In, ApiMessageRole.Observe);

    private static ApiStateEffect ReadEffect() =>
        new(GameRequirement.Habbicons, ApiStateChange.Reads);

    private static ApiStateEffect ChangeEffect() =>
        new(GameRequirement.Habbicons, ApiStateChange.Changes);
}

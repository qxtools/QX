using Qx.Game.Api;
using Qx.Protocol;

namespace Qx.Game.Subscriptions;

internal static class SubscriptionDescriptors
{
    private static readonly ApiExposure event_exposure =
        ApiExposure.Ui | ApiExposure.Cli | ApiExposure.Scripting;

    public static ApiDescriptor State { get; } = new(
        ApiIds.SubscriptionsState,
        "Subscription state",
        "Reads a bounded immutable page of subscription products and the latest passive club-offer, kickback and Builders Club summaries.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(SubscriptionStateRequest),
        typeof(SubscriptionStateView),
        StateParameters(),
        stateEffects:
        [new(GameRequirement.Subscriptions, ApiStateChange.Reads)],
        messages: ObservedMessages(),
        toolHints: new(true, false, true, false),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor ClubOffersList { get; } = new(
        ApiIds.SubscriptionsClubOffersList,
        "Club offers page",
        "Reads a bounded page from one immutable club-offer snapshot without assigning it to an offer type.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(SubscriptionClubOffersPageRequest),
        typeof(SubscriptionClubOffersPage),
        ClubOffersPageParameters(),
        stateEffects:
        [new(GameRequirement.Subscriptions, ApiStateChange.Reads)],
        messages:
        [
            new(
                MessageKeys.Subscriptions.ClubOffersSnapshot,
                MessageDirection.In,
                ApiMessageRole.Observe)
        ],
        toolHints: new(true, false, true, false),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor ClubOffersRefresh { get; } = new(
        ApiIds.SubscriptionsClubOffersRefresh,
        "Refresh club offers",
        "Requests club offers and returns the first bounded page of the first fresh route snapshot observed after dispatch without claiming offer-type identity.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(SubscriptionClubOffersRefreshRequest),
        typeof(SubscriptionClubOffersPage),
        [
            new(
                "offer_type",
                typeof(int),
                false,
                1,
                "Offer-set selector sent to the hotel without being treated as response identity."),
            ClubOffersLimitParameter(),
            ClubOffersTimeoutParameter(),
            ExpectedGenerationParameter()
        ],
        [GameRequirement.HotelConnected],
        [new(GameRequirement.Subscriptions, ApiStateChange.Changes)],
        [
            new(
                MessageKeys.Subscriptions.ClubOffersRequest,
                MessageDirection.Out,
                ApiMessageRole.Send),
            new(
                MessageKeys.Subscriptions.ClubOffersSnapshot,
                MessageDirection.In,
                ApiMessageRole.Observe)
        ],
        new(true, false, true, true));

    public static ApiDescriptor UserInfoRefresh { get; } = new(
        ApiIds.SubscriptionsUserInfoRefresh,
        "Refresh subscription user info",
        "Requests one product and returns the matching fresh response observed after dispatch without claiming request identity.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(SubscriptionUserInfoRefreshRequest),
        typeof(SubscriptionUserInfoRefreshResult),
        [ProductNameParameter(true), TimeoutParameter(), ExpectedGenerationParameter()],
        [GameRequirement.HotelConnected],
        [new(GameRequirement.Subscriptions, ApiStateChange.Changes)],
        [
            new(
                MessageKeys.Subscriptions.UserInfoRequest,
                MessageDirection.Out,
                ApiMessageRole.Send),
            new(
                MessageKeys.Subscriptions.UserInfo,
                MessageDirection.In,
                ApiMessageRole.Observe)
        ],
        new(true, false, true, true));

    public static ApiDescriptor KickbackRefresh { get; } = new(
        ApiIds.SubscriptionsKickbackRefresh,
        "Refresh subscription kickback",
        "Requests the kickback summary and returns the fresh route response observed after dispatch without claiming request identity.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(SubscriptionKickbackRefreshRequest),
        typeof(SubscriptionKickbackRefreshResult),
        [TimeoutParameter(), ExpectedGenerationParameter()],
        [GameRequirement.HotelConnected],
        [new(GameRequirement.Subscriptions, ApiStateChange.Changes)],
        [
            new(
                MessageKeys.Subscriptions.KickbackInfoRequest,
                MessageDirection.Out,
                ApiMessageRole.Send),
            new(
                MessageKeys.Subscriptions.KickbackInfo,
                MessageDirection.In,
                ApiMessageRole.Observe)
        ],
        new(true, false, true, true));

    public static ApiDescriptor BuildersClubFurniCountRefresh { get; } = new(
        ApiIds.SubscriptionsBuildersClubFurniCountRefresh,
        "Refresh Builders Club furni count",
        "Requests the Builders Club furni count and returns the fresh route response observed after dispatch without claiming request identity.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(BuildersClubCountRequest),
        typeof(BuildersClubCountResult),
        [TimeoutParameter(), ExpectedGenerationParameter()],
        [GameRequirement.HotelConnected],
        [new(GameRequirement.Subscriptions, ApiStateChange.Changes)],
        [
            new(
                MessageKeys.Subscriptions.BuildersClubFurniCountRequest,
                MessageDirection.Out,
                ApiMessageRole.Send),
            new(
                MessageKeys.Subscriptions.BuildersClubFurniCount,
                MessageDirection.In,
                ApiMessageRole.Observe)
        ],
        new(true, false, true, true));

    public static ApiDescriptor BuildersClubFloorOfferPlace { get; } = new(
        ApiIds.SubscriptionsBuildersClubFloorOfferPlace,
        "Place Builders Club floor offer",
        "Dispatches one Builders Club floor-offer placement in the current room without claiming hotel acceptance.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(BuildersClubFloorPlaceRequest),
        typeof(BuildersClubDispatch),
        [
            RequiredInt("page_id", "Catalog page identifier sent unchanged."),
            RequiredInt("offer_id", "Builders Club offer identifier sent unchanged."),
            RequiredInt("x", "Target tile column sent unchanged."),
            RequiredInt("y", "Target tile row sent unchanged."),
            new("direction", typeof(int), false, 0, "Target direction sent unchanged."),
            ExtraDataParameter(),
            new("is_retry", typeof(bool), false, false, "Explicit hotel placement-retry flag."),
            ExpectedGenerationParameter(),
            ExpectedRoomGenerationParameter()
        ],
        [GameRequirement.HotelConnected, GameRequirement.RoomReady],
        [new(GameRequirement.RoomActive, ApiStateChange.Reads)],
        [
            new(
                MessageKeys.Subscriptions.BuildersClubFloorOfferPlace,
                MessageDirection.Out,
                ApiMessageRole.Send)
        ],
        new(false, true, false, true));

    public static ApiDescriptor BuildersClubWallOfferPlace { get; } = new(
        ApiIds.SubscriptionsBuildersClubWallOfferPlace,
        "Place Builders Club wall offer",
        "Dispatches one Builders Club wall-offer placement in the current room without claiming hotel acceptance.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(BuildersClubWallPlaceRequest),
        typeof(BuildersClubDispatch),
        [
            RequiredInt("page_id", "Catalog page identifier sent unchanged."),
            RequiredInt("offer_id", "Builders Club offer identifier sent unchanged."),
            new(
                "wall_location",
                typeof(string),
                true,
                null,
                "Non-empty wall-location string in the Flash text form.",
                new(MinLength: 1, MaxUtf8Bytes: ushort.MaxValue, Pattern: @".*\S.*")),
            ExtraDataParameter(),
            new("is_retry", typeof(bool), false, false, "Explicit hotel placement-retry flag."),
            ExpectedGenerationParameter(),
            ExpectedRoomGenerationParameter()
        ],
        [GameRequirement.HotelConnected, GameRequirement.RoomReady],
        [new(GameRequirement.RoomActive, ApiStateChange.Reads)],
        [
            new(
                MessageKeys.Subscriptions.BuildersClubWallOfferPlace,
                MessageDirection.Out,
                ApiMessageRole.Send)
        ],
        new(false, true, false, true));

    public static ApiDescriptor Changed { get; } = new(
        ApiIds.SubscriptionsChanged,
        "Subscription state changed",
        "Publishes ordered bounded subscription, kickback, Builders Club and reset changes.",
        ApiMemberKind.Event,
        event_exposure,
        null,
        typeof(SubscriptionChanged),
        stateEffects:
        [new(GameRequirement.Subscriptions, ApiStateChange.Changes)],
        messages: ObservedMessages(),
        invocationScope: ApiLifetime.Persistent);

    private static IReadOnlyList<ApiParameter> StateParameters() =>
    [
        ProductNameParameter(false),
        new(
            "offset",
            typeof(int),
            false,
            0,
            "Zero-based offset within the immutable product snapshot.",
            new(Minimum: 0)),
        new(
            "limit",
            typeof(int),
            false,
            100,
            "Maximum products returned by this page.",
            new(Minimum: 1, Maximum: 500)),
        new(
            "snapshot_revision",
            typeof(long?),
            false,
            null,
            "Snapshot revision returned by the first page and required for continuation pages.",
            new(Minimum: 1))
    ];

    private static IReadOnlyList<ApiParameter> ClubOffersPageParameters() =>
    [
        new(
            "offset",
            typeof(int),
            false,
            0,
            "Zero-based offset within the immutable club-offer snapshot.",
            new(Minimum: 0)),
        ClubOffersLimitParameter(),
        new(
            "snapshot_revision",
            typeof(long?),
            false,
            null,
            "Snapshot revision returned by the first page and required for continuation pages.",
            new(Minimum: 1))
    ];

    private static IReadOnlyList<ApiMessageRequirement> ObservedMessages() =>
    [
        new(
            MessageKeys.Subscriptions.UserInfo,
            MessageDirection.In,
            ApiMessageRole.Observe),
        new(
            MessageKeys.Subscriptions.KickbackInfo,
            MessageDirection.In,
            ApiMessageRole.Observe),
        new(
            MessageKeys.Subscriptions.ClubOffersSnapshot,
            MessageDirection.In,
            ApiMessageRole.Observe),
        new(
            MessageKeys.Subscriptions.BuildersClubFurniCount,
            MessageDirection.In,
            ApiMessageRole.Observe),
        new(
            MessageKeys.Subscriptions.BuildersClubMembershipStatus,
            MessageDirection.In,
            ApiMessageRole.Observe,
            false),
        new(
            MessageKeys.Subscriptions.BuildersClubPlacementWarning,
            MessageDirection.In,
            ApiMessageRole.Observe,
            false)
    ];

    private static ApiParameter ProductNameParameter(bool refresh) => new(
        "product_name",
        typeof(string),
        false,
        refresh ? "habbo_club" : null,
        refresh
            ? "Non-empty subscription product name sent to the hotel."
            : "Optional case-insensitive exact product filter.",
        new(MinLength: 1, MaxUtf8Bytes: ushort.MaxValue, Pattern: @".*\S.*"));

    private static ApiParameter TimeoutParameter() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum total time across both attempts.",
        new(Minimum: 1, Maximum: 120000));

    private static ApiParameter ClubOffersLimitParameter() => new(
        "limit",
        typeof(int),
        false,
        100,
        "Maximum club offers returned by this page.",
        new(Minimum: 1, Maximum: 500));

    private static ApiParameter ClubOffersTimeoutParameter() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum time for the single request attempt.",
        new(Minimum: 1));

    private static ApiParameter RequiredInt(
        string name,
        string description) => new(name, typeof(int), true, null, description);

    private static ApiParameter ExtraDataParameter() => new(
        "extra_data",
        typeof(string),
        false,
        string.Empty,
        "Offer selection data.",
        new(MaxUtf8Bytes: ushort.MaxValue));

    private static ApiParameter ExpectedGenerationParameter() => new(
        "expected_session_generation",
        typeof(long?),
        false,
        null,
        "Optional active session generation precondition.",
        new(Minimum: 1));

    private static ApiParameter ExpectedRoomGenerationParameter() => new(
        "expected_room_generation",
        typeof(long?),
        false,
        null,
        "Optional active room generation precondition.",
        new(Minimum: 1));
}

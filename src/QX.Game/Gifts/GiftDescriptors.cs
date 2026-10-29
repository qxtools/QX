using Qx.Game.Api;
using Qx.Game.Catalog;
using Qx.Game.Messages;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
using Qx.Protocol;

namespace Qx.Game.Gifts;

internal static class GiftDescriptors
{
    private static readonly ApiExposure event_exposure =
        ApiExposure.Ui | ApiExposure.Cli | ApiExposure.Scripting;

    public static ApiDescriptor State { get; } = new(
        ApiIds.GiftsState,
        "Gift state",
        "Reads one atomic bounded summary of gift state without retaining nested gift graphs.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(GiftStateRequest),
        typeof(GiftStateView),
        stateEffects: [GiftRead()],
        messages: ObservedMessages(),
        toolHints: QueryHints(),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor WrappingList { get; } = new(
        ApiIds.GiftsWrappingList,
        "Gift wrapping page",
        "Reads one bounded collection from an immutable gift snapshot.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(GiftWrappingPageRequest),
        typeof(GiftWrappingPage),
        PageParameters(typeof(GiftWrappingCollection), GiftWrappingCollection.StuffTypes),
        stateEffects: [GiftRead()],
        messages:
        [
            new(
                MessageKeys.Gifts.WrappingConfiguration,
                MessageDirection.In,
                ApiMessageRole.Observe)
        ],
        toolHints: QueryHints(),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor ClubInfoList { get; } = new(
        ApiIds.GiftsClubInfoList,
        "Club gift page",
        "Reads one flat bounded collection from an immutable club-gift snapshot.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(GiftClubInfoPageRequest),
        typeof(GiftClubInfoPage),
        PageParameters(typeof(GiftClubInfoCollection), GiftClubInfoCollection.Offers),
        stateEffects: [GiftRead()],
        messages:
        [
            new(
                MessageKeys.Gifts.ClubInfo,
                MessageDirection.In,
                ApiMessageRole.Observe)
        ],
        toolHints: QueryHints(),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor ClubSelectedList { get; } = new(
        ApiIds.GiftsClubSelectedList,
        "Selected club gift page",
        "Reads one bounded collection from the last immutable selected club-gift snapshot.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(GiftClubSelectedPageRequest),
        typeof(GiftClubSelectedPage),
        PageParameters(
            typeof(GiftClubSelectedCollection),
            GiftClubSelectedCollection.Products),
        stateEffects: [GiftRead()],
        messages:
        [
            new(
                MessageKeys.Gifts.ClubSelected,
                MessageDirection.In,
                ApiMessageRole.Observe)
        ],
        toolHints: QueryHints(),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor NewUserOfferList { get; } = new(
        ApiIds.GiftsNewUserOfferList,
        "New-user gift offer page",
        "Reads one flat bounded collection from the last immutable new-user gift offer.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(GiftNewUserOfferPageRequest),
        typeof(GiftNewUserOfferPage),
        PageParameters(typeof(GiftNewUserOfferCollection), GiftNewUserOfferCollection.Steps),
        stateEffects: [GiftRead()],
        messages:
        [
            new(
                MessageKeys.Gifts.NewUserOffer,
                MessageDirection.In,
                ApiMessageRole.Observe)
        ],
        toolHints: QueryHints(),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor Refresh { get; } = new(
        ApiIds.GiftsRefresh,
        "Refresh gift configuration",
        "Requests wrapping and club-gift state on one pinned session and returns the first fresh committed response from each route without claiming a request identifier.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(GiftRefreshRequest),
        typeof(GiftRefreshResult),
        [LimitParameter(), TimeoutParameter(), ExpectedSessionGenerationParameter()],
        [GameRequirement.HotelConnected],
        [GiftChange()],
        [
            new(
                MessageKeys.Gifts.WrappingConfigurationRequest,
                MessageDirection.Out,
                ApiMessageRole.Send),
            new(
                MessageKeys.Gifts.WrappingConfiguration,
                MessageDirection.In,
                ApiMessageRole.Observe),
            new(
                MessageKeys.Gifts.ClubInfoRequest,
                MessageDirection.Out,
                ApiMessageRole.Send),
            new(
                MessageKeys.Gifts.ClubInfo,
                MessageDirection.In,
                ApiMessageRole.Observe)
        ],
        RefreshHints());

    public static ApiDescriptor PresentOpen { get; } = new(
        ApiIds.GiftsPresentOpen,
        "Open present",
        "Dispatches one room-local present-open request without claiming an acknowledgement.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(GiftPresentOpenRequest),
        typeof(GiftPresentOpenDispatchReceipt),
        [
            new(
                "furni_id",
                typeof(Id),
                true,
                null,
                "Positive room-furniture identifier.",
                new(Pattern: "^[1-9][0-9]*$")),
            ExpectedSessionGenerationParameter(),
            ExpectedRoomGenerationParameter()
        ],
        [GameRequirement.HotelConnected, GameRequirement.RoomReady],
        [GiftRead(), new(GameRequirement.RoomActive, ApiStateChange.Reads)],
        [
            new(
                MessageKeys.Gifts.PresentOpen,
                MessageDirection.Out,
                ApiMessageRole.Send),
            new(
                MessageKeys.Gifts.PresentOpened,
                MessageDirection.In,
                ApiMessageRole.Observe,
                false)
        ],
        DispatchHints());

    public static ApiDescriptor Purchase { get; } = new(
        ApiIds.GiftsPurchase,
        "Purchase gift",
        "Dispatches one gift purchase on a pinned hotel session and catalog generation without claiming a purchase outcome.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(GiftPurchaseRequest),
        typeof(GiftPurchaseDispatchReceipt),
        PurchaseParameters(),
        [GameRequirement.HotelConnected],
        [
            GiftRead(),
            new(GameRequirement.CatalogCache, ApiStateChange.Reads)
        ],
        [
            new(
                MessageKeys.Gifts.Purchase,
                MessageDirection.Out,
                ApiMessageRole.Send),
            new(
                MessageKeys.Gifts.ReceiverNotFound,
                MessageDirection.In,
                ApiMessageRole.Observe,
                false)
        ],
        DispatchHints());

    public static ApiDescriptor ClubSelect { get; } = new(
        ApiIds.GiftsClubSelect,
        "Select club gift",
        "Dispatches one club-gift selection without claiming hotel acceptance.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(GiftClubSelectRequest),
        typeof(GiftClubSelectDispatchReceipt),
        [
            RequiredString("product_code", "Non-empty club-gift product code."),
            ExpectedSessionGenerationParameter(),
            ExpectedRevisionParameter(
                "expected_club_info_revision",
                "Optional club-info revision pinned through dispatch.")
        ],
        [GameRequirement.HotelConnected],
        [GiftRead()],
        [
            new(
                MessageKeys.Gifts.ClubSelect,
                MessageDirection.Out,
                ApiMessageRole.Send),
            new(
                MessageKeys.Gifts.ClubSelected,
                MessageDirection.In,
                ApiMessageRole.Observe,
                false)
        ],
        DispatchHints());

    public static ApiDescriptor OfferGiftabilityRefresh { get; } = new(
        ApiIds.GiftsOfferGiftabilityRefresh,
        "Refresh offer giftability",
        "Asks whether a catalog offer can be sent as a gift. Requests one offer and returns the exact matching fresh Flash response.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(GiftOfferGiftabilityRefreshRequest),
        typeof(GiftOfferGiftabilityRefreshResult),
        [
            Required("offer_id", typeof(int), "Offer identifier matched exactly."),
            TimeoutParameter(),
            ExpectedSessionGenerationParameter()
        ],
        [GameRequirement.HotelConnected],
        [GiftChange()],
        [
            new(
                MessageKeys.Gifts.OfferGiftabilityRequest,
                MessageDirection.Out,
                ApiMessageRole.Send),
            new(
                MessageKeys.Gifts.OfferGiftability,
                MessageDirection.In,
                ApiMessageRole.Observe)
        ],
        RefreshHints());

    public static ApiDescriptor NewUserSelect { get; } = new(
        ApiIds.GiftsNewUserSelect,
        "Select new-user gifts",
        "Dispatches one bounded new-user gift selection without claiming hotel acceptance.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(GiftNewUserSelectRequest),
        typeof(GiftNewUserSelectDispatchReceipt),
        [
            new(
                "selections",
                typeof(IReadOnlyList<NuxGiftSelection>),
                true,
                null,
                "Ordered new-user gift selections.",
                new(MinItems: 0, MaxItems: 21845)),
            ExpectedSessionGenerationParameter(),
            ExpectedRevisionParameter(
                "expected_new_user_offer_revision",
                "Optional new-user offer revision pinned through dispatch.")
        ],
        [GameRequirement.HotelConnected],
        [GiftRead()],
        [
            new(
                MessageKeys.Gifts.NewUserSelect,
                MessageDirection.Out,
                ApiMessageRole.Send),
            new(
                MessageKeys.Gifts.NewUserIncomplete,
                MessageDirection.In,
                ApiMessageRole.Observe,
                false)
        ],
        DispatchHints());

    public static ApiDescriptor NewUserAdvance { get; } = new(
        ApiIds.GiftsNewUserAdvance,
        "Advance new-user gift flow",
        "Dispatches one room-local new-user flow advance without claiming an acknowledgement.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(GiftNewUserAdvanceRequest),
        typeof(GiftNewUserAdvanceDispatchReceipt),
        [ExpectedSessionGenerationParameter(), ExpectedRoomGenerationParameter()],
        [GameRequirement.HotelConnected, GameRequirement.RoomReady],
        [GiftRead(), new(GameRequirement.RoomActive, ApiStateChange.Reads)],
        [
            new(
                MessageKeys.Gifts.NewUserAdvance,
                MessageDirection.Out,
                ApiMessageRole.Send),
            new(
                MessageKeys.Gifts.NewUserIncomplete,
                MessageDirection.In,
                ApiMessageRole.Observe,
                false)
        ],
        DispatchHints());

    public static ApiDescriptor Changed { get; } = new(
        ApiIds.GiftsChanged,
        "Gift state changed",
        "Publishes ordered bounded gift summaries and reset changes.",
        ApiMemberKind.Event,
        event_exposure,
        null,
        typeof(GiftChanged),
        stateEffects: [GiftChange()],
        messages: ObservedMessages(),
        invocationScope: ApiLifetime.Persistent);

    private static IReadOnlyList<ApiParameter> PageParameters(
        Type collection_type,
        object default_collection) =>
    [
        new(
            "collection",
            collection_type,
            false,
            default_collection,
            "Collection selected from the immutable gift snapshot."),
        new(
            "offset",
            typeof(int),
            false,
            0,
            "Zero-based offset within the selected collection.",
            new(Minimum: 0)),
        LimitParameter(),
        new(
            "snapshot_revision",
            typeof(long?),
            false,
            null,
            "Snapshot revision returned by the first page and required for continuation pages.",
            new(Minimum: 1))
    ];

    private static IReadOnlyList<ApiParameter> PurchaseParameters() =>
    [
        Required("page_id", typeof(int), "Catalog page identifier sent unchanged."),
        Required("offer_id", typeof(int), "Catalog offer identifier sent unchanged."),
        WireString("extra_data", false, "Catalog extra data sent unchanged."),
        WireString("receiver_name", false, "Gift receiver name sent unchanged."),
        WireString("gift_message", false, "Gift message sent unchanged."),
        Required("sprite_id", typeof(int), "Gift sprite identifier sent in the first wrapping slot."),
        Required("box_type", typeof(int), "Gift box type sent in the second wrapping slot."),
        Required("ribbon_type", typeof(int), "Gift ribbon type sent in the third wrapping slot."),
        Required(
            "show_purchaser_name",
            typeof(bool),
            "Wire flag controlling whether the purchaser name is shown."),
        new(
            "quantity",
            typeof(int),
            false,
            1,
            "Requested quantity; each gift purchase sends one item.",
            new(Minimum: 1)),
        ExpectedSessionGenerationParameter(),
        ExpectedRevisionParameter(
            "expected_catalog_generation",
            "Optional catalog generation pinned through dispatch.")
    ];

    private static IReadOnlyList<ApiMessageRequirement> ObservedMessages() =>
    [
        new(
            MessageKeys.Gifts.WrappingConfiguration,
            MessageDirection.In,
            ApiMessageRole.Observe),
        new(
            MessageKeys.Gifts.PresentOpened,
            MessageDirection.In,
            ApiMessageRole.Observe),
        new(
            MessageKeys.Gifts.ClubInfo,
            MessageDirection.In,
            ApiMessageRole.Observe),
        new(
            MessageKeys.Gifts.ClubSelected,
            MessageDirection.In,
            ApiMessageRole.Observe),
        new(
            MessageKeys.Gifts.ReceiverNotFound,
            MessageDirection.In,
            ApiMessageRole.Observe,
            false),
        new(
            MessageKeys.Gifts.ClubNotification,
            MessageDirection.In,
            ApiMessageRole.Observe,
            false),
        new(
            MessageKeys.Gifts.OfferGiftability,
            MessageDirection.In,
            ApiMessageRole.Observe,
            false),
        new(
            MessageKeys.Gifts.NewUserOffer,
            MessageDirection.In,
            ApiMessageRole.Observe,
            false),
        new(
            MessageKeys.Gifts.NewUserIncomplete,
            MessageDirection.In,
            ApiMessageRole.Observe)
    ];

    private static ApiParameter Required(
        string name,
        Type type,
        string description) => new(name, type, true, null, description);

    private static ApiParameter RequiredString(
        string name,
        string description) => new(
        name,
        typeof(string),
        true,
        null,
        description,
        new(MinLength: 1, MaxUtf8Bytes: ushort.MaxValue, Pattern: @".*\S.*"));

    private static ApiParameter WireString(
        string name,
        bool non_empty,
        string description) => new(
        name,
        typeof(string),
        true,
        null,
        description,
        new(
            MinLength: non_empty ? 1 : null,
            MaxUtf8Bytes: ushort.MaxValue,
            Pattern: non_empty ? @".*\S.*" : null));

    private static ApiParameter LimitParameter() => new(
        "limit",
        typeof(int),
        false,
        100,
        "Maximum rows returned from one flat collection.",
        new(Minimum: 1, Maximum: 500));

    private static ApiParameter TimeoutParameter() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum total wait for the operation.",
        new(Minimum: 1, Maximum: 120000));

    private static ApiParameter ExpectedSessionGenerationParameter() =>
        ExpectedRevisionParameter(
            "expected_session_generation",
            "Optional active hotel-session generation required through dispatch.");

    private static ApiParameter ExpectedRoomGenerationParameter() =>
        ExpectedRevisionParameter(
            "expected_room_generation",
            "Optional ready-room generation required through dispatch.");

    private static ApiParameter ExpectedRevisionParameter(
        string name,
        string description) => new(
        name,
        typeof(long?),
        false,
        null,
        description,
        new(Minimum: 1));

    private static ApiStateEffect GiftRead() =>
        new(GameRequirement.Gifts, ApiStateChange.Reads);

    private static ApiStateEffect GiftChange() =>
        new(GameRequirement.Gifts, ApiStateChange.Changes);

    private static ApiToolHints QueryHints() => new(true, false, true, false);
    private static ApiToolHints RefreshHints() => new(true, false, true, true);
    private static ApiToolHints DispatchHints() => new(false, true, false, true);
}

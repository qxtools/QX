using System.Diagnostics.CodeAnalysis;
using Qx.Game.Api;
using Qx.Model.Messages.Incoming;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Game.Marketplace;

internal static class MarketplaceDescriptors
{
    private static readonly ApiExposure event_exposure =
        ApiExposure.Ui | ApiExposure.Cli | ApiExposure.Scripting;

    public static ApiDescriptor State { get; } = new(
        ApiIds.MarketplaceState,
        "Marketplace state",
        "Reads one bounded page of the active session's immutable marketplace state.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(MarketplaceStateRequest),
        typeof(MarketplaceStateView),
        PagingParameters(),
        stateEffects:
        [
            new(GameRequirement.MarketplaceConfigurationLoaded, ApiStateChange.Reads),
            new(GameRequirement.MarketplaceEligibilityLoaded, ApiStateChange.Reads)
        ],
        messages: StateMessages(),
        toolHints: new(true, false, true, false),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor ConfigurationRefresh { get; } = RequestResponse<
        MarketplaceRefreshRequest,
        MarketplaceConfiguration>(
        ApiIds.MarketplaceConfigurationRefresh,
        "Refresh marketplace configuration",
        "Loads marketplace availability, pricing, fee and tax limits for the active hotel.",
        MessageKeys.Marketplace.Configuration.Request,
        MessageKeys.Marketplace.Configuration.Snapshot,
        [TimeoutParameter()],
        new(true, false, true, true),
        [new(GameRequirement.MarketplaceConfigurationLoaded, ApiStateChange.Changes)]);

    public static ApiDescriptor EligibilityRefresh { get; } = RequestResponse<
        MarketplaceRefreshRequest,
        MarketplaceCanMakeOfferResult>(
        ApiIds.MarketplaceEligibilityRefresh,
        "Refresh marketplace eligibility",
        "Loads whether the active account may create an offer and why it may be refused.",
        MessageKeys.Marketplace.Eligibility.Request,
        MessageKeys.Marketplace.Eligibility.Result,
        [TimeoutParameter()],
        new(true, false, true, true),
        [new(GameRequirement.MarketplaceEligibilityLoaded, ApiStateChange.Changes)]);

    public static ApiDescriptor ItemStatsGet { get; } = RequestResponse<
        MarketplaceItemStatsRequest,
        MarketplaceItemStatsSnapshot>(
        ApiIds.MarketplaceItemStatsGet,
        "Get marketplace item statistics",
        "Loads current offers and bounded historical sale statistics for one furniture type.",
        MessageKeys.Marketplace.ItemStats.Request,
        MessageKeys.Marketplace.ItemStats.Snapshot,
        [
            new("furni_category", typeof(MarketplaceFurniCategory), true, null, "Furniture market category."),
            new("furni_type_id", typeof(int), true, null, "Furniture type or limited-edition lookup identifier.", new(Minimum: 1)),
            TextParameter("extra_data", "Optional variant data used by modern marketplace layouts."),
            TimeoutParameter()
        ],
        new(true, false, true, true));

    public static ApiDescriptor Search { get; } = RequestResponse<
        MarketplaceSearchRequest,
        MarketplaceOfferPage>(
        ApiIds.MarketplaceSearch,
        "Search marketplace",
        "Searches public marketplace offers and returns one bounded page of the hotel result.",
        MessageKeys.Marketplace.Offers.SearchRequest,
        MessageKeys.Marketplace.Offers.SearchResult,
        [
            TextParameter("search_query", "Furniture name or marketplace search text."),
            new("minimum_price", typeof(int), false, -1, "Minimum price, or -1 for no lower bound.", new(Minimum: -1)),
            new("maximum_price", typeof(int), false, -1, "Maximum price, or -1 for no upper bound.", new(Minimum: -1)),
            new("sort_order", typeof(MarketplaceSortOrder), false, MarketplaceSortOrder.HighestPrice, "Hotel marketplace sort order."),
            new("combine_unique_offers", typeof(bool), false, true, "Groups equivalent offers when the active Flash layout supports it."),
            .. PagingParameters(),
            TimeoutParameter()
        ],
        new(true, false, true, true));

    public static ApiDescriptor OwnOffersGet { get; } = RequestResponse<
        MarketplaceOwnOffersRequest,
        MarketplaceOwnOfferPage>(
        ApiIds.MarketplaceOwnOffersGet,
        "Get own marketplace offers",
        "Loads one bounded page of the active account's open, sold or expired offers.",
        MessageKeys.Marketplace.Offers.OwnRequest,
        MessageKeys.Marketplace.Offers.OwnSnapshot,
        [
            new("category", typeof(MarketplaceOwnOffersCategory), false, MarketplaceOwnOffersCategory.Open, "Own-offer category; legacy Flash expose only open offers."),
            .. PagingParameters(),
            TimeoutParameter()
        ],
        new(true, false, true, true));

    public static ApiDescriptor OfferMake { get; } = RequestResponse<
        MarketplaceMakeOfferRequest,
        MarketplaceMakeOfferResult>(
        ApiIds.MarketplaceOfferMake,
        "Create marketplace offer",
        "Lists one or more inventory items for sale using the verified Flash marketplace layout.",
        MessageKeys.Marketplace.Offers.Make,
        MessageKeys.Marketplace.Offers.MakeResult,
        [
            new("price", typeof(int), true, null, "Listing price per offer.", new(Minimum: 1)),
            new("furni_category", typeof(MarketplaceSellCategory), true, null, "Floor or wall furniture category."),
            new("item_ids", typeof(IReadOnlyList<Id>), true, null, "Distinct inventory item identifiers.", new(MinItems: 1, MaxItems: 1000)),
            TimeoutParameter()
        ],
        new(false, true, false, true));

    public static ApiDescriptor OfferBuy { get; } = RequestResponse<
        MarketplaceBuyRequest,
        MarketplaceBuyResult>(
        ApiIds.MarketplaceOfferBuy,
        "Buy marketplace offer",
        "Purchases an offer by ID and waits for the hotel result.",
        MessageKeys.Marketplace.Offers.Buy,
        MessageKeys.Marketplace.Offers.BuyResult,
        [OfferIdParameter(), TimeoutParameter()],
        new(false, true, false, true));

    public static ApiDescriptor OfferBuySend { get; } = Send<MarketplaceBuySendRequest>(
        ApiIds.MarketplaceOfferBuySend,
        "Send marketplace purchase",
        "Sends a purchase for a cached offer without waiting for the hotel result.",
        MessageKeys.Marketplace.Offers.Buy,
        [OfferIdParameter()],
        new(false, true, false, true));

    public static ApiDescriptor OfferCancel { get; } = RequestResponse<
        MarketplaceCancelRequest,
        MarketplaceCancelOfferResult>(
        ApiIds.MarketplaceOfferCancel,
        "Cancel marketplace offer",
        "Cancels one own offer and waits for the verified Flash result.",
        MessageKeys.Marketplace.Offers.Cancel,
        MessageKeys.Marketplace.Offers.CancelResult,
        [OfferIdParameter(), TimeoutParameter()],
        new(false, true, false, true));

    public static ApiDescriptor OfferCancelSend { get; } = Send<MarketplaceCancelSendRequest>(
        ApiIds.MarketplaceOfferCancelSend,
        "Send marketplace cancellation",
        "Sends one offer cancellation without waiting for an unverified client result.",
        MessageKeys.Marketplace.Offers.Cancel,
        [OfferIdParameter()],
        new(false, true, false, true));

    public static ApiDescriptor OffersCancelAll { get; } = RequestResponse<
        MarketplaceCancelAllRequest,
        MarketplaceCancelAllOffersSnapshot>(
        ApiIds.MarketplaceOffersCancelAll,
        "Cancel all marketplace offers",
        "Cancels every open offer and returns the exact identifiers confirmed by the hotel.",
        MessageKeys.Marketplace.Offers.CancelAll,
        MessageKeys.Marketplace.Offers.CancelAllResult,
        [TimeoutParameter()],
        new(false, true, false, true));

    public static ApiDescriptor HistoryClear { get; } = RequestResponse<
        MarketplaceHistoryClearRequest,
        MarketplaceClearOwnHistoryResult>(
        ApiIds.MarketplaceHistoryClear,
        "Clear marketplace history",
        "Clears the sold or expired own-offer history on supported Flash layouts.",
        MessageKeys.Marketplace.Offers.ClearOwnHistory,
        MessageKeys.Marketplace.Offers.ClearOwnHistoryResult,
        [
            new("category", typeof(MarketplaceHistoryCategory), true, null, "Sold or expired own-offer category."),
            TimeoutParameter()
        ],
        new(false, true, false, true));

    public static ApiDescriptor CreditsRedeem { get; } = Send<MarketplaceCommandRequest>(
        ApiIds.MarketplaceCreditsRedeem,
        "Redeem marketplace credits",
        "Collects credits waiting on sold marketplace offers.",
        MessageKeys.Marketplace.Credits.Redeem,
        [],
        new(false, true, false, true));

    public static ApiDescriptor TokensBuy { get; } = Send<MarketplaceCommandRequest>(
        ApiIds.MarketplaceTokensBuy,
        "Buy marketplace tokens",
        "Buys the configured marketplace listing-token batch for the active account.",
        MessageKeys.Marketplace.Tokens.Buy,
        [],
        new(false, true, false, true));

    public static ApiDescriptor Changed { get; } = Event<MarketplaceChanged>(
        ApiIds.MarketplaceChanged,
        "Marketplace state changed",
        "Publishes ordered immutable marketplace state revisions, including reset.",
        StateMessages(),
        [
            new(GameRequirement.MarketplaceConfigurationLoaded, ApiStateChange.Changes),
            new(GameRequirement.MarketplaceEligibilityLoaded, ApiStateChange.Changes)
        ]);

    public static ApiDescriptor ConfigurationChanged { get; } = Event<MarketplaceConfigurationChanged>(
        ApiIds.MarketplaceConfigurationChanged,
        "Marketplace configuration changed",
        "Publishes marketplace configuration received for the active session.",
        [new(MessageKeys.Marketplace.Configuration.Snapshot, MessageDirection.In, ApiMessageRole.Observe)]);

    public static ApiDescriptor EligibilityChanged { get; } = Event<MarketplaceEligibilityChanged>(
        ApiIds.MarketplaceEligibilityChanged,
        "Marketplace eligibility changed",
        "Publishes the active account's latest marketplace eligibility result.",
        [new(MessageKeys.Marketplace.Eligibility.Result, MessageDirection.In, ApiMessageRole.Observe)]);

    public static ApiDescriptor SearchReceived { get; } = Event<MarketplaceSearchReceived>(
        ApiIds.MarketplaceSearchReceived,
        "Marketplace search received",
        "Publishes a bounded first page of each public marketplace search result.",
        [new(MessageKeys.Marketplace.Offers.SearchResult, MessageDirection.In, ApiMessageRole.Observe)]);

    public static ApiDescriptor OwnOffersReceived { get; } = Event<MarketplaceOwnOffersReceived>(
        ApiIds.MarketplaceOwnOffersReceived,
        "Own marketplace offers received",
        "Publishes a bounded first page of each own-offer snapshot.",
        [new(MessageKeys.Marketplace.Offers.OwnSnapshot, MessageDirection.In, ApiMessageRole.Observe)]);

    public static ApiDescriptor ItemStatsReceived { get; } = Event<MarketplaceItemStatsReceived>(
        ApiIds.MarketplaceItemStatsReceived,
        "Marketplace item statistics received",
        "Publishes immutable sale statistics received for one furniture type.",
        [new(MessageKeys.Marketplace.ItemStats.Snapshot, MessageDirection.In, ApiMessageRole.Observe)]);

    public static ApiDescriptor MakeResultReceived { get; } = Event<MarketplaceMakeOfferResultReceived>(
        ApiIds.MarketplaceOfferMakeResult,
        "Marketplace offer result",
        "Publishes each hotel result for a marketplace listing request.",
        [new(MessageKeys.Marketplace.Offers.MakeResult, MessageDirection.In, ApiMessageRole.Observe)]);

    public static ApiDescriptor BuyResultReceived { get; } = Event<MarketplaceBuyResultReceived>(
        ApiIds.MarketplaceOfferBuyResult,
        "Marketplace purchase result",
        "Publishes each hotel result for a marketplace purchase.",
        [new(MessageKeys.Marketplace.Offers.BuyResult, MessageDirection.In, ApiMessageRole.Observe)]);

    public static ApiDescriptor CancelResultReceived { get; } = Event<MarketplaceCancelResultReceived>(
        ApiIds.MarketplaceOfferCancelResult,
        "Marketplace cancellation result",
        "Publishes verified Flash cancellation results.",
        [new(MessageKeys.Marketplace.Offers.CancelResult, MessageDirection.In, ApiMessageRole.Observe)]);

    public static ApiDescriptor CancelAllResultReceived { get; } = Event<MarketplaceCancelAllResultReceived>(
        ApiIds.MarketplaceOffersCancelAllResult,
        "Marketplace cancel-all result",
        "Publishes immutable cancel-all results and confirmed offer identifiers.",
        [new(MessageKeys.Marketplace.Offers.CancelAllResult, MessageDirection.In, ApiMessageRole.Observe)]);

    public static ApiDescriptor HistoryClearResultReceived { get; } = Event<MarketplaceHistoryClearResultReceived>(
        ApiIds.MarketplaceHistoryClearResult,
        "Marketplace history-clear result",
        "Publishes verified Flash history-clear results.",
        [new(MessageKeys.Marketplace.Offers.ClearOwnHistoryResult, MessageDirection.In, ApiMessageRole.Observe)]);

    private static ApiDescriptor RequestResponse<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRequest, TResult>(
        string id,
        string title,
        string description,
        MessageKey request_key,
        MessageKey response_key,
        IReadOnlyList<ApiParameter> parameters,
        ApiToolHints hints,
        IReadOnlyList<ApiStateEffect>? state_effects = null) => new(
        id,
        title,
        description,
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(TRequest),
        typeof(TResult),
        parameters,
        [GameRequirement.HotelConnected],
        state_effects,
        [
            new(request_key, MessageDirection.Out, ApiMessageRole.Send),
            new(response_key, MessageDirection.In, ApiMessageRole.Observe)
        ],
        hints);

    private static ApiDescriptor Send<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRequest>(
        string id,
        string title,
        string description,
        MessageKey request_key,
        IReadOnlyList<ApiParameter> parameters,
        ApiToolHints hints) => new(
        id,
        title,
        description,
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(TRequest),
        typeof(MarketplaceDispatchResult),
        parameters,
        [GameRequirement.HotelConnected],
        messages: [new(request_key, MessageDirection.Out, ApiMessageRole.Send)],
        toolHints: hints);

    private static ApiDescriptor Event<TEvent>(
        string id,
        string title,
        string description,
        IReadOnlyList<ApiMessageRequirement> messages,
        IReadOnlyList<ApiStateEffect>? state_effects = null) => new(
        id,
        title,
        description,
        ApiMemberKind.Event,
        event_exposure,
        null,
        typeof(TEvent),
        stateEffects: state_effects,
        messages: messages);

    private static ApiParameter[] PagingParameters() =>
    [
        new("page", typeof(int), false, 0, "Zero-based result page.", new(Minimum: 0)),
        new("page_size", typeof(int), false, 100, "Maximum records returned in this page.", new(Minimum: 1, Maximum: 250))
    ];

    private static ApiParameter TimeoutParameter() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum time to wait for the hotel response.",
        new(Minimum: 1, Maximum: 120000));

    private static ApiParameter TextParameter(
        string name,
        string description) => new(
        name,
        typeof(string),
        false,
        string.Empty,
        description,
        new(MaxUtf8Bytes: ushort.MaxValue));

    private static ApiParameter OfferIdParameter() => new(
        "offer_id",
        typeof(Id),
        true,
        null,
        "Positive marketplace offer identifier.",
        new(Pattern: @"^[1-9][0-9]*$"));

    private static ApiMessageRequirement[] StateMessages() =>
    [
        new(MessageKeys.Marketplace.Configuration.Snapshot, MessageDirection.In, ApiMessageRole.Observe, false),
        new(MessageKeys.Marketplace.Eligibility.Result, MessageDirection.In, ApiMessageRole.Observe, false),
        new(MessageKeys.Marketplace.Offers.SearchResult, MessageDirection.In, ApiMessageRole.Observe, false),
        new(MessageKeys.Marketplace.Offers.OwnSnapshot, MessageDirection.In, ApiMessageRole.Observe, false),
        new(MessageKeys.Marketplace.ItemStats.Snapshot, MessageDirection.In, ApiMessageRole.Observe, false),
        new(MessageKeys.Marketplace.Offers.MakeResult, MessageDirection.In, ApiMessageRole.Observe, false),
        new(MessageKeys.Marketplace.Offers.BuyResult, MessageDirection.In, ApiMessageRole.Observe, false),
        new(MessageKeys.Marketplace.Offers.CancelResult, MessageDirection.In, ApiMessageRole.Observe, false),
        new(MessageKeys.Marketplace.Offers.CancelAllResult, MessageDirection.In, ApiMessageRole.Observe, false),
        new(MessageKeys.Marketplace.Offers.ClearOwnHistoryResult, MessageDirection.In, ApiMessageRole.Observe, false)
    ];
}

using Qx.Game.Api;
using Qx.Protocol;

namespace Qx.Game.Catalog;

internal static class CatalogDescriptors
{
    private static readonly ApiExposure event_exposure =
        ApiExposure.Ui | ApiExposure.Cli | ApiExposure.Scripting;

    public static ApiDescriptor State { get; } = new(
        ApiIds.CatalogState,
        "Catalog state",
        "Reads the bounded cache state for one catalog type.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(CatalogStateRequest),
        typeof(CatalogStateView),
        [CatalogTypeParameter()],
        stateEffects: [CacheEffect(ApiStateChange.Reads)],
        messages: [PublishedMessage(false)],
        toolHints: new(true, false, true, false),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor IndexGet { get; } = new(
        ApiIds.CatalogIndexGet,
        "Get catalog index",
        "Loads the active catalog index and returns a bounded flat projection.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(CatalogIndexGetRequest),
        typeof(CatalogIndexView),
        [CatalogTypeParameter(), MaxAgeParameter(), TimeoutParameter(), .. GenerationParameters()],
        [GameRequirement.HotelConnected],
        [
            CacheEffect(ApiStateChange.Changes),
            CacheEffect(ApiStateChange.Invalidates)
        ],
        messages:
        [
            new(MessageKeys.Catalog.IndexRequest, MessageDirection.Out, ApiMessageRole.Send),
            new(MessageKeys.Catalog.IndexSnapshot, MessageDirection.In, ApiMessageRole.Observe),
            PublishedMessage(false)
        ],
        toolHints: new(true, false, true, true));

    public static ApiDescriptor PageGet { get; } = new(
        ApiIds.CatalogPageGet,
        "Get catalog page",
        "Loads one catalog page and returns a compact bounded offer projection.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(CatalogPageGetRequest),
        typeof(CatalogPageView),
        [
            new("page_id", typeof(int), true, null, "Non-negative catalog page identifier.", new(Minimum: 0)),
            new("offer_id", typeof(int), false, -1, "Offer to preselect, or -1.", new(Minimum: -1)),
            CatalogTypeParameter(),
            MaxAgeParameter(),
            TimeoutParameter(),
            .. GenerationParameters()
        ],
        [GameRequirement.HotelConnected],
        [CacheEffect(ApiStateChange.Changes)],
        messages:
        [
            new(MessageKeys.Catalog.PageRequest, MessageDirection.Out, ApiMessageRole.Send),
            new(MessageKeys.Catalog.PageSnapshot, MessageDirection.In, ApiMessageRole.Observe),
            PublishedMessage(false)
        ],
        toolHints: new(true, false, true, true));

    public static ApiDescriptor PagesLoad { get; } = new(
        ApiIds.CatalogPagesLoad,
        "Load catalog pages",
        "Walks the current index and caches every eligible offer page without mixing catalog generations.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(CatalogLoadRequest),
        typeof(CatalogLoadView),
        [
            CatalogTypeParameter(),
            new("only_visible", typeof(bool), false, true, "Skips hidden page subtrees."),
            new("delay_milliseconds", typeof(int), false, 0, "Pause between page requests.", new(Minimum: 0)),
            MaxAgeParameter(),
            TimeoutParameter(15000, "Maximum time for the index and for each individual page response."),
            .. GenerationParameters()
        ],
        [GameRequirement.HotelConnected],
        [
            CacheEffect(ApiStateChange.Changes),
            CacheEffect(ApiStateChange.Invalidates)
        ],
        messages:
        [
            new(MessageKeys.Catalog.IndexRequest, MessageDirection.Out, ApiMessageRole.Send),
            new(MessageKeys.Catalog.IndexSnapshot, MessageDirection.In, ApiMessageRole.Observe),
            new(MessageKeys.Catalog.PageRequest, MessageDirection.Out, ApiMessageRole.Send),
            new(MessageKeys.Catalog.PageSnapshot, MessageDirection.In, ApiMessageRole.Observe),
            PublishedMessage(false)
        ],
        toolHints: new(true, false, true, true));

    public static ApiDescriptor PagesList { get; } = new(
        ApiIds.CatalogPagesList,
        "Cached catalog pages",
        "Reads one stable bounded page of compact cached-page summaries.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(CatalogPagesRequest),
        typeof(CatalogPageListView),
        [CatalogTypeParameter(), .. PagingParameters(), .. GenerationParameters()],
        stateEffects: [CacheEffect(ApiStateChange.Reads)],
        messages: [PublishedMessage(false)],
        toolHints: new(true, false, true, false),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor OffersSearch { get; } = new(
        ApiIds.CatalogOffersSearch,
        "Search cached catalog offers",
        "Searches compact cached offers with a stable bounded snapshot lease; empty text lists all cached offers.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(CatalogOfferSearchRequest),
        typeof(CatalogOfferSearchPage),
        [
            new("text", typeof(string), false, string.Empty, "Case-insensitive cached-offer text filter.", new(MaxUtf8Bytes: 1024)),
            CatalogTypeParameter(),
            .. PagingParameters(),
            .. GenerationParameters()
        ],
        stateEffects: [CacheEffect(ApiStateChange.Reads)],
        messages: [PublishedMessage(false)],
        toolHints: new(true, false, true, false),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor CacheClear { get; } = new(
        ApiIds.CatalogCacheClear,
        "Clear catalog cache",
        "Atomically invalidates one catalog type or the complete catalog cache.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(CatalogCacheClearRequest),
        typeof(CatalogCacheClearView),
        [NullableCatalogTypeParameter(), .. GenerationParameters()],
        stateEffects: [CacheEffect(ApiStateChange.Invalidates)],
        messages: [PublishedMessage(false)],
        toolHints: new(false, true, true, false),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor PurchaseState { get; } = new(
        ApiIds.CatalogPurchaseState,
        "Catalog purchase state",
        "Reads the latest passive catalog purchase outcome for the active hotel session.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(CatalogPurchaseStateRequest),
        typeof(CatalogPurchaseStateView),
        stateEffects:
        [
            new(GameRequirement.CatalogPurchase, ApiStateChange.Reads)
        ],
        messages: PurchaseOutcomeMessages(false),
        toolHints: new(true, false, true, false),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor PurchaseSend { get; } = new(
        ApiIds.CatalogPurchaseSend,
        "Send catalog purchase",
        "Dispatches one catalog purchase without claiming that a later global outcome belongs to it.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(CatalogPurchaseSendRequest),
        typeof(CatalogPurchaseDispatchReceipt),
        [
            new("page_id", typeof(int), true, null, "Non-negative catalog page identifier.", new(Minimum: 0)),
            new("offer_id", typeof(int), true, null, "Non-negative catalog offer identifier.", new(Minimum: 0)),
            new("extra_data", typeof(string), false, string.Empty, "Offer selection data.", new(MaxUtf8Bytes: ushort.MaxValue)),
            new("quantity", typeof(int), false, 1, "Positive purchase quantity.", new(Minimum: 1)),
            .. GenerationParameters()
        ],
        [GameRequirement.HotelConnected],
        messages:
        [
            new(MessageKeys.Catalog.Purchase, MessageDirection.Out, ApiMessageRole.Send)
        ],
        toolHints: new(false, true, false, true));

    public static ApiDescriptor PurchaseOutcome { get; } = new(
        ApiIds.CatalogPurchaseOutcome,
        "Catalog purchase outcome",
        "Publishes each passive hotel purchase outcome without correlating it to a dispatch.",
        ApiMemberKind.Event,
        event_exposure,
        null,
        typeof(CatalogPurchaseOutcomeEvent),
        stateEffects:
        [
            new(GameRequirement.CatalogPurchase, ApiStateChange.Changes)
        ],
        messages: PurchaseOutcomeMessages(),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor Published { get; } = new(
        ApiIds.CatalogPublished,
        "Catalog published",
        "Publishes the committed invalidation epoch before consumers can observe stale cache state.",
        ApiMemberKind.Event,
        event_exposure,
        null,
        typeof(CatalogPublishedEvent),
        stateEffects: [CacheEffect(ApiStateChange.Invalidates)],
        messages: [PublishedMessage()]);

    private static ApiParameter CatalogTypeParameter() => new(
        "catalog_type",
        typeof(string),
        false,
        "NORMAL",
        "Catalog mode; NORMAL and BUILDERS_CLUB are accepted case-insensitively.");

    private static ApiParameter NullableCatalogTypeParameter() => new(
        "catalog_type",
        typeof(string),
        false,
        null,
        "Catalog mode, or null for every mode; names are case-insensitive.");

    private static ApiParameter MaxAgeParameter() => new(
        "max_age_milliseconds",
        typeof(long),
        false,
        300000L,
        "Cache age limit; zero forces a refresh and -1 accepts any age.",
        new(Minimum: -1, Maximum: TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerMillisecond));

    private static ApiParameter TimeoutParameter(
        int value = 10000,
        string description = "Maximum time to wait for the response.") => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        value,
        description,
        new(Minimum: 1, Maximum: 120000));

    private static ApiParameter[] PagingParameters() =>
    [
        new("offset", typeof(int), false, 0, "Zero-based snapshot offset.", new(Minimum: 0)),
        new("limit", typeof(int), false, 100, "Maximum rows returned.", new(Minimum: 1, Maximum: 500)),
        new("snapshot_revision", typeof(long?), false, null, "Opaque continuation lease from the first page.", new(Minimum: 1))
    ];

    private static ApiParameter[] GenerationParameters() =>
    [
        new("expected_session_generation", typeof(long?), false, null, "Optional session compare-and-swap guard.", new(Minimum: 0)),
        new("expected_catalog_generation", typeof(long?), false, null, "Optional catalog compare-and-swap guard.", new(Minimum: 0))
    ];

    private static ApiMessageRequirement PublishedMessage(bool required = true) => new(
        MessageKeys.Catalog.Published,
        MessageDirection.In,
        ApiMessageRole.Observe,
        required);

    private static IReadOnlyList<ApiMessageRequirement> PurchaseOutcomeMessages(
        bool required = true) =>
    [
        new(MessageKeys.Catalog.PurchaseAccepted, MessageDirection.In, ApiMessageRole.Observe, required),
        new(MessageKeys.Catalog.PurchaseFailed, MessageDirection.In, ApiMessageRole.Observe, required),
        new(MessageKeys.Catalog.PurchaseForbidden, MessageDirection.In, ApiMessageRole.Observe, required)
    ];

    private static ApiStateEffect CacheEffect(ApiStateChange kind) =>
        new(GameRequirement.CatalogCache, kind);
}

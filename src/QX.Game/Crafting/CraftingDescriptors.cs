using Qx.Game.Api;
using Qx.Game.Messages;
using Qx.Protocol;

namespace Qx.Game.Crafting;

internal static class CraftingDescriptors
{
    private static readonly ApiExposure event_exposure =
        ApiExposure.Ui |
        ApiExposure.Cli |
        ApiExposure.Scripting;

    public static ApiDescriptor State { get; } = new(
        ApiIds.CraftingState,
        "Crafting state",
        "Reads one bounded state view from a current or retained immutable crafting snapshot.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(CraftingStateRequest),
        typeof(CraftingStateView),
        [SnapshotRevisionParameter(false)],
        stateEffects: [CraftingRead()],
        messages: ObservedMessages(),
        toolHints: QueryHints(),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor ProductsList { get; } = new(
        ApiIds.CraftingProductsList,
        "Crafting products page",
        "Reads one bounded products or usable-furniture-class page from an immutable crafting snapshot.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(CraftingProductsPageRequest),
        typeof(CraftingProductsPage),
        ProductsPageParameters(),
        stateEffects: [CraftingRead()],
        messages:
        [
            Observe(MessageKeys.Crafting.ProductsSnapshot)
        ],
        toolHints: QueryHints(),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor RecipeList { get; } = new(
        ApiIds.CraftingRecipeList,
        "Crafting recipe page",
        "Reads one bounded ingredient page from an immutable crafting snapshot.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(CraftingRecipePageRequest),
        typeof(CraftingRecipePage),
        PageParameters(),
        stateEffects: [CraftingRead()],
        messages:
        [
            Observe(MessageKeys.Crafting.RecipeSnapshot)
        ],
        toolHints: QueryHints(),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor ProductsRefresh { get; } = new(
        ApiIds.CraftingProductsRefresh,
        "Refresh crafting products",
        "Requests products in one pinned room and returns the first fresh post-dispatch route response only when no intervening crafting-products request invalidates its request epoch; the response does not echo the crafting-furniture id, so RequestedCraftingFurnitureId is request metadata only.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(CraftingProductsRefreshRequest),
        typeof(CraftingProductsRefreshResult),
        [
            FurnitureIdParameter(),
            LimitParameter(),
            TimeoutParameter(),
            ExpectedSessionGenerationParameter(),
            ExpectedRoomGenerationParameter()
        ],
        OperationStates(),
        [CraftingChange(), RoomRead()],
        [
            Send(MessageKeys.Crafting.ProductsRequest),
            Observe(MessageKeys.Crafting.ProductsSnapshot)
        ],
        RefreshHints());

    public static ApiDescriptor RecipeRefresh { get; } = new(
        ApiIds.CraftingRecipeRefresh,
        "Refresh crafting recipe",
        "Requests one recipe in a pinned room and returns the first fresh post-dispatch route response only when no intervening crafting-recipe request invalidates its request epoch; the response does not echo the recipe code, so RequestedRecipeCode is request metadata only.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(CraftingRecipeRefreshRequest),
        typeof(CraftingRecipeRefreshResult),
        [
            RecipeCodeParameter(),
            LimitParameter(),
            TimeoutParameter(),
            ExpectedSessionGenerationParameter(),
            ExpectedRoomGenerationParameter()
        ],
        OperationStates(),
        [CraftingChange(), RoomRead()],
        [
            Send(MessageKeys.Crafting.RecipeRequest),
            Observe(MessageKeys.Crafting.RecipeSnapshot)
        ],
        RefreshHints());

    public static ApiDescriptor AvailabilityRefresh { get; } = new(
        ApiIds.CraftingAvailabilityRefresh,
        "Refresh crafting availability",
        "Requests recipe availability in one pinned room and returns the first fresh post-dispatch route response only when no intervening availability request invalidates its request epoch; the response does not echo the request values, so the Requested fields are request metadata only.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(CraftingAvailabilityRefreshRequest),
        typeof(CraftingAvailabilityRefreshResult),
        [
            FurnitureIdParameter(),
            IngredientIdsParameter(),
            TimeoutParameter(),
            ExpectedSessionGenerationParameter(),
            ExpectedRoomGenerationParameter()
        ],
        OperationStates(),
        [CraftingChange(), RoomRead()],
        [
            Send(MessageKeys.Crafting.AvailabilityRequest),
            Observe(MessageKeys.Crafting.AvailabilitySnapshot)
        ],
        RefreshHints());

    public static ApiDescriptor Craft { get; } = new(
        ApiIds.CraftingCraft,
        "Craft recipe",
        "Dispatches one known-recipe craft request in a pinned room without awaiting or assigning a crafting result.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(CraftingCraftRequest),
        typeof(CraftingCraftDispatchReceipt),
        [
            FurnitureIdParameter(),
            RecipeCodeParameter(),
            ExpectedSessionGenerationParameter(),
            ExpectedRoomGenerationParameter()
        ],
        OperationStates(),
        [CraftingRead(), RoomRead()],
        [
            Send(MessageKeys.Crafting.Craft),
            Observe(MessageKeys.Crafting.Result, false)
        ],
        DispatchHints());

    public static ApiDescriptor SecretCraft { get; } = new(
        ApiIds.CraftingSecretCraft,
        "Craft secret recipe",
        "Dispatches one secret-craft request in a pinned room without awaiting or assigning a crafting result.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(CraftingSecretCraftRequest),
        typeof(CraftingSecretCraftDispatchReceipt),
        [
            FurnitureIdParameter(),
            IngredientIdsParameter(),
            ExpectedSessionGenerationParameter(),
            ExpectedRoomGenerationParameter()
        ],
        OperationStates(),
        [CraftingRead(), RoomRead()],
        [
            Send(MessageKeys.Crafting.SecretCraft),
            Observe(MessageKeys.Crafting.Result, false)
        ],
        DispatchHints());

    public static ApiDescriptor Changed { get; } = new(
        ApiIds.CraftingChanged,
        "Crafting changed",
        "Publishes bounded passive crafting-state changes without assigning result messages to craft requests.",
        ApiMemberKind.Event,
        event_exposure,
        null,
        typeof(CraftingChanged),
        stateEffects: [CraftingChange()],
        messages: ObservedMessages(),
        invocationScope: ApiLifetime.Persistent);

    private static IReadOnlyList<ApiParameter>
        ProductsPageParameters() =>
    [
        new(
            "collection",
            typeof(CraftingProductsCollection),
            false,
            CraftingProductsCollection.Products,
            "Collection selected from the immutable products snapshot."),
        .. PageParameters()
    ];

    private static IReadOnlyList<ApiParameter> PageParameters() =>
    [
        new(
            "offset",
            typeof(int),
            false,
            0,
            "Zero-based offset within the selected collection.",
            new(Minimum: 0)),
        LimitParameter(),
        SnapshotRevisionParameter(true)
    ];

    private static IReadOnlyList<GameRequirement> OperationStates() =>
    [
        GameRequirement.HotelConnected,
        GameRequirement.RoomReady
    ];

    private static IReadOnlyList<ApiMessageRequirement> ObservedMessages() =>
    [
        Observe(MessageKeys.Crafting.ProductsSnapshot),
        Observe(MessageKeys.Crafting.RecipeSnapshot),
        Observe(MessageKeys.Crafting.Result),
        Observe(MessageKeys.Crafting.AvailabilitySnapshot)
    ];

    private static ApiParameter FurnitureIdParameter() => new(
        "crafting_furniture_id",
        typeof(Id),
        true,
        null,
        "Positive crafting-furniture identifier valid for Flash.",
        new(Pattern: "^[1-9][0-9]*$"));

    private static ApiParameter RecipeCodeParameter() => new(
        "recipe_code",
        typeof(string),
        true,
        null,
        "Nonblank recipe code within the wire-string budget.",
        new(
            MinLength: 1,
            MaxUtf8Bytes: ushort.MaxValue,
            Pattern: @".*\S.*"));

    private static ApiParameter IngredientIdsParameter() => new(
        "ingredient_item_ids",
        typeof(IReadOnlyList<Id>),
        true,
        null,
        "Ingredient item identifiers copied before dispatch.",
        new(MinItems: 0, MaxItems: ushort.MaxValue));

    private static ApiParameter LimitParameter() => new(
        "limit",
        typeof(int),
        false,
        100,
        "Maximum rows returned from one collection.",
        new(Minimum: 1, Maximum: 500));

    private static ApiParameter TimeoutParameter() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum total wait for the first fresh route response.",
        new(Minimum: 1, Maximum: 120000));

    private static ApiParameter SnapshotRevisionParameter(
        bool continuation) => new(
        "snapshot_revision",
        typeof(long?),
        false,
        null,
        continuation
            ? "Snapshot revision returned by the first read and required for continuation pages."
            : "Optional retained snapshot revision; omitted to capture the current state.",
        new(Minimum: 1));

    private static ApiParameter ExpectedSessionGenerationParameter() =>
        ExpectedRevisionParameter(
            "expected_session_generation",
            "Optional active hotel-session generation required through dispatch.");

    private static ApiParameter ExpectedRoomGenerationParameter() =>
        ExpectedRevisionParameter(
            "expected_room_generation",
            "Optional room generation required through dispatch.");

    private static ApiParameter ExpectedRevisionParameter(
        string name,
        string description) => new(
        name,
        typeof(long?),
        false,
        null,
        description,
        new(Minimum: 1));

    private static ApiMessageRequirement Send(MessageKey key) =>
        new(key, MessageDirection.Out, ApiMessageRole.Send);

    private static ApiMessageRequirement Observe(
        MessageKey key,
        bool required = true) =>
        new(key, MessageDirection.In, ApiMessageRole.Observe, required);

    private static ApiStateEffect CraftingRead() =>
        new(GameRequirement.Crafting, ApiStateChange.Reads);

    private static ApiStateEffect CraftingChange() =>
        new(GameRequirement.Crafting, ApiStateChange.Changes);

    private static ApiStateEffect RoomRead() =>
        new(GameRequirement.RoomActive, ApiStateChange.Reads);

    private static ApiToolHints QueryHints() => new(true, false, true, false);
    private static ApiToolHints RefreshHints() => new(true, false, true, true);
    private static ApiToolHints DispatchHints() => new(false, true, false, true);
}

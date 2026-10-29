using Qx.Game.Api;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Game.Inventory;

internal static class InventoryDescriptors
{
    private static readonly ApiExposure event_exposure =
        ApiExposure.Ui | ApiExposure.Cli | ApiExposure.Scripting;

    public static ApiDescriptor State { get; } = new(
        ApiIds.InventoryState,
        "Inventory state",
        "Reads immutable furni and pet inventory lifecycle summaries for the active session.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(InventoryStateRequest),
        typeof(InventoryStateView),
        [],
        stateEffects:
        [
            new(GameRequirement.InventoryFurniLoaded, ApiStateChange.Reads),
            new(GameRequirement.InventoryPetsLoaded, ApiStateChange.Reads)
        ],
        messages: StateMessages(),
        toolHints: new(true, false, true, false),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor FurniList { get; } = new(
        ApiIds.InventoryFurniList,
        "Furni inventory page",
        "Reads a bounded page from one immutable furni inventory snapshot.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(InventoryFurniPageRequest),
        typeof(InventoryFurniPage),
        FurniPageParameters(),
        stateEffects:
        [new(GameRequirement.InventoryFurniLoaded, ApiStateChange.Reads)],
        messages: FurniMessages(),
        toolHints: new(true, false, true, false),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor FurniRefresh { get; } = new(
        ApiIds.InventoryFurniRefresh,
        "Refresh furni inventory",
        "Loads every fragment for the active session and returns the first bounded snapshot page.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(InventoryFurniRefreshRequest),
        typeof(InventoryFurniPage),
        [OptionalId("item_id", "Optional exact inventory item identifier."), LimitParameter(), TimeoutParameter()],
        [GameRequirement.HotelConnected],
        [new(GameRequirement.InventoryFurniLoaded, ApiStateChange.Changes)],
        [
            new(MessageKeys.Inventory.Furni.Request, MessageDirection.Out, ApiMessageRole.Send),
            new(MessageKeys.Inventory.Furni.Snapshot, MessageDirection.In, ApiMessageRole.Observe)
        ],
        new(true, false, true, true));

    public static ApiDescriptor PetsList { get; } = new(
        ApiIds.InventoryPetsList,
        "Pet inventory page",
        "Reads a bounded page from one immutable pet inventory snapshot with optional combined exact filters.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(InventoryPetPageRequest),
        typeof(InventoryPetPage),
        PetPageParameters(),
        stateEffects:
        [new(GameRequirement.InventoryPetsLoaded, ApiStateChange.Reads)],
        messages: PetMessages(),
        toolHints: new(true, false, true, false),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor PetsRefresh { get; } = new(
        ApiIds.InventoryPetsRefresh,
        "Refresh pet inventory",
        "Loads every fragment for the active session and returns the first bounded snapshot page.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(InventoryPetRefreshRequest),
        typeof(InventoryPetPage),
        [
            OptionalId("pet_id", "Optional exact pet identifier; combined with name when both are present."),
            NameParameter(),
            LimitParameter(),
            TimeoutParameter()
        ],
        [GameRequirement.HotelConnected],
        [new(GameRequirement.InventoryPetsLoaded, ApiStateChange.Changes)],
        [
            new(MessageKeys.Inventory.Pets.Request, MessageDirection.Out, ApiMessageRole.Send),
            new(MessageKeys.Inventory.Pets.Snapshot, MessageDirection.In, ApiMessageRole.Observe)
        ],
        new(true, false, true, true));

    public static ApiDescriptor AvatarEffectActivate { get; } = new(
        ApiIds.InventoryAvatarEffectActivate,
        "Activate avatar effect",
        "Activates an owned avatar effect through Flash.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(InventoryAvatarEffectRequest),
        typeof(InventoryDispatchResult),
        [new("effect_id", typeof(int), true, null, "Avatar effect identifier.")],
        [GameRequirement.HotelConnected],
        messages:
        [
            new(
                MessageKeys.Inventory.AvatarEffects.ActivationRequest,
                MessageDirection.Out,
                ApiMessageRole.Send)
        ],
        toolHints: new(false, true, false, true));

    public static ApiDescriptor FurniChanged { get; } = new(
        ApiIds.InventoryFurniChanged,
        "Furni inventory changed",
        "Publishes bounded immutable furni load, invalidation, add, update, remove and reset changes.",
        ApiMemberKind.Event,
        event_exposure,
        null,
        typeof(InventoryFurniChanged),
        stateEffects:
        [new(GameRequirement.InventoryFurniLoaded, ApiStateChange.Changes)],
        messages: FurniMessages());

    public static ApiDescriptor PetsChanged { get; } = new(
        ApiIds.InventoryPetsChanged,
        "Pet inventory changed",
        "Publishes bounded immutable pet load, add, update, remove and reset changes.",
        ApiMemberKind.Event,
        event_exposure,
        null,
        typeof(InventoryPetChanged),
        stateEffects:
        [new(GameRequirement.InventoryPetsLoaded, ApiStateChange.Changes)],
        messages: PetMessages());

    private static IReadOnlyList<ApiParameter> FurniPageParameters() =>
    [
        OptionalId("item_id", "Optional exact inventory item identifier."),
        OffsetParameter(),
        LimitParameter(),
        SnapshotRevisionParameter()
    ];

    private static IReadOnlyList<ApiParameter> PetPageParameters() =>
    [
        OptionalId("pet_id", "Optional exact pet identifier; combined with name when both are present."),
        NameParameter(),
        OffsetParameter(),
        LimitParameter(),
        SnapshotRevisionParameter()
    ];

    private static IReadOnlyList<ApiMessageRequirement> StateMessages() =>
        [.. FurniMessages(), .. PetMessages()];

    private static IReadOnlyList<ApiMessageRequirement> FurniMessages() =>
    [
        Observe(MessageKeys.Inventory.Furni.Snapshot),
        Observe(MessageKeys.Inventory.Furni.AddedOrUpdated),
        Observe(MessageKeys.Inventory.Furni.Removed),
        Observe(MessageKeys.Inventory.Furni.RemovedMultiple, false),
        Observe(MessageKeys.Inventory.Furni.Invalidated),
        Observe(MessageKeys.Inventory.Furni.PostItPlaced)
    ];

    private static IReadOnlyList<ApiMessageRequirement> PetMessages() =>
    [
        Observe(MessageKeys.Inventory.Pets.Snapshot),
        Observe(MessageKeys.Inventory.Pets.Added),
        Observe(MessageKeys.Inventory.Pets.Removed)
    ];

    private static ApiMessageRequirement Observe(MessageKey key, bool required = true) =>
        new(key, MessageDirection.In, ApiMessageRole.Observe, required);

    private static ApiParameter OptionalId(string name, string description) =>
        new(name, typeof(Id?), false, null, description, new(Pattern: "^[1-9][0-9]*$"));

    private static ApiParameter NameParameter() => new(
        "name",
        typeof(string),
        false,
        null,
        "Optional case-insensitive exact pet name; combined with pet_id when both are present.",
        new(MinLength: 1, MaxUtf8Bytes: ushort.MaxValue, Pattern: @".*\S.*"));

    private static ApiParameter OffsetParameter() => new(
        "offset",
        typeof(int),
        false,
        0,
        "Zero-based offset within the selected immutable snapshot.",
        new(Minimum: 0));

    private static ApiParameter LimitParameter() => new(
        "limit",
        typeof(int),
        false,
        200,
        "Maximum entries returned by this page.",
        new(Minimum: 1, Maximum: 500));

    private static ApiParameter SnapshotRevisionParameter() => new(
        "snapshot_revision",
        typeof(long?),
        false,
        null,
        "Snapshot revision returned by the first page and required for continuation pages.",
        new(Minimum: 1));

    private static ApiParameter TimeoutParameter() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum total time to wait for every fragment.",
        new(Minimum: 1, Maximum: 120000));
}

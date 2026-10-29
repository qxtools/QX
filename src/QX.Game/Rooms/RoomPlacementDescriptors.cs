using System.Diagnostics.CodeAnalysis;
using Qx.Game.Api;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Game.Rooms;

internal static class RoomPlacementDescriptors
{
    private static readonly ApiExposure event_exposure =
        ApiExposure.Ui | ApiExposure.Cli | ApiExposure.Scripting;

    public static ApiDescriptor FloorPlace { get; } = new(
        ApiIds.RoomPlacementFloorPlace,
        "Place floor item",
        "Dispatches one inventory floor item placement without assuming hotel acceptance.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(RoomPlacementFloorPlaceRequest),
        typeof(RoomPlacementDispatchReceipt),
        [
            InventoryItemId(),
            new("target", typeof(RoomPlacementFloorPosition), true, null, "Floor tile and direction."),
            ExpectedRoomItemId(),
            ExpectedInventoryRevision(),
            SessionGeneration(),
            RoomGeneration()
        ],
        [
            GameRequirement.HotelConnected,
            GameRequirement.RoomReady,
            GameRequirement.InventoryFurniLoaded
        ],
        [
            new(GameRequirement.RoomActive, ApiStateChange.Reads),
            new(GameRequirement.InventoryFurniLoaded, ApiStateChange.Reads)
        ],
        [Send(MessageKeys.Room.Item.Place)],
        new(false, false, false, true));

    public static ApiDescriptor WallPlace { get; } = new(
        ApiIds.RoomPlacementWallPlace,
        "Place wall item",
        "Dispatches one inventory wall item placement without assuming hotel acceptance.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(RoomPlacementWallPlaceRequest),
        typeof(RoomPlacementDispatchReceipt),
        [
            InventoryItemId(),
            new("target", typeof(RoomPlacementWallPosition), true, null, "Wall and offset coordinates with orientation."),
            ExpectedRoomItemId(),
            ExpectedInventoryRevision(),
            SessionGeneration(),
            RoomGeneration()
        ],
        [
            GameRequirement.HotelConnected,
            GameRequirement.RoomReady,
            GameRequirement.InventoryFurniLoaded
        ],
        [
            new(GameRequirement.RoomActive, ApiStateChange.Reads),
            new(GameRequirement.InventoryFurniLoaded, ApiStateChange.Reads)
        ],
        [Send(MessageKeys.Room.Item.Place)],
        new(false, false, false, true));

    public static ApiDescriptor FloorMove { get; } = Move<RoomPlacementFloorMoveRequest>(
        ApiIds.RoomPlacementFloorMove,
        "Move floor item",
        "Dispatches one current floor item move without assuming hotel acceptance.",
        MessageKeys.Room.FloorItem.Move,
        typeof(RoomPlacementFloorPosition));

    public static ApiDescriptor WallMove { get; } = Move<RoomPlacementWallMoveRequest>(
        ApiIds.RoomPlacementWallMove,
        "Move wall item",
        "Dispatches one current wall item move without assuming hotel acceptance.",
        MessageKeys.Room.WallItem.Move,
        typeof(RoomPlacementWallPosition));

    public static ApiDescriptor Pickup { get; } = new(
        ApiIds.RoomPlacementPickup,
        "Pick up room item",
        "Dispatches one current room-item pickup without assuming hotel acceptance.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(RoomPlacementPickupRequest),
        typeof(RoomPlacementDispatchReceipt),
        [
            RoomItemId(),
            new("item_kind", typeof(RoomPlacementItemKind), true, null, "Expected floor or wall item kind."),
            new("confirmed", typeof(bool), false, false, "Flash pickup-confirmation response."),
            SessionGeneration(),
            RoomGeneration()
        ],
        [GameRequirement.HotelConnected, GameRequirement.RoomReady],
        [new(GameRequirement.RoomActive, ApiStateChange.Reads)],
        [Send(MessageKeys.Room.Item.Pickup)],
        new(false, true, false, true));

    public static ApiDescriptor Changed { get; } = new(
        ApiIds.RoomPlacementChanged,
        "Room placement changed",
        "Publishes immutable single-item placement changes and room lifecycle invalidation.",
        ApiMemberKind.Event,
        event_exposure,
        null,
        typeof(RoomPlacementChanged),
        stateEffects:
        [
            new(GameRequirement.RoomActive, ApiStateChange.Changes),
            new(GameRequirement.RoomActive, ApiStateChange.Invalidates)
        ],
        messages:
        [
            Observe(MessageKeys.Room.FloorItem.Added),
            Observe(MessageKeys.Room.FloorItem.Updated),
            Observe(MessageKeys.Room.FloorItem.Removed),
            Observe(MessageKeys.Room.WallItem.Added),
            Observe(MessageKeys.Room.WallItem.Updated),
            Observe(MessageKeys.Room.WallItem.Removed)
        ]);

    public static ApiDescriptor PickupConfirmation { get; } = new(
        ApiIds.RoomPlacementPickupConfirmation,
        "Room-item pickup confirmation",
        "Publishes the bounded Flash pickup-confirmation prompt without treating it as an acknowledgement.",
        ApiMemberKind.Event,
        event_exposure,
        null,
        typeof(RoomPlacementPickupConfirmation),
        messages: [Observe(MessageKeys.Room.Item.PickupConfirmation)]);

    private static ApiDescriptor Move<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRequest>(
        string id,
        string title,
        string description,
        MessageKey key,
        Type position_type) => new(
        id,
        title,
        description,
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(TRequest),
        typeof(RoomPlacementDispatchReceipt),
        [
            RoomItemId(),
            new("target", position_type, true, null, "Target room position."),
            new("expected_source", position_type, false, null, "Optional exact source-position guard."),
            SessionGeneration(),
            RoomGeneration()
        ],
        [GameRequirement.HotelConnected, GameRequirement.RoomReady],
        [new(GameRequirement.RoomActive, ApiStateChange.Reads)],
        [Send(key)],
        new(false, false, false, true));

    private static ApiParameter InventoryItemId() => new(
        "inventory_item_id",
        typeof(Id),
        true,
        null,
        "Nonzero inventory item identifier.",
        IdConstraint());

    private static ApiParameter RoomItemId() => new(
        "room_item_id",
        typeof(Id),
        true,
        null,
        "Nonzero current room-item identifier.",
        IdConstraint());

    private static ApiParameter ExpectedRoomItemId() => new(
        "expected_room_item_id",
        typeof(Id?),
        false,
        null,
        "Optional expected room-item identifier mapped by the inventory snapshot.",
        IdConstraint());

    private static ApiParameter ExpectedInventoryRevision() => new(
        "expected_inventory_revision",
        typeof(long?),
        false,
        null,
        "Optional exact immutable furni-inventory snapshot revision.",
        new(Minimum: 0));

    private static ApiParameter SessionGeneration() => new(
        "expected_session_generation",
        typeof(long?),
        false,
        null,
        "Optional active hotel-session generation guard.",
        new(Minimum: 0));

    private static ApiParameter RoomGeneration() => new(
        "expected_room_generation",
        typeof(long?),
        false,
        null,
        "Optional ready-room generation guard.",
        new(Minimum: 0));

    private static ApiConstraints IdConstraint() =>
        new(Pattern: "^-?[1-9][0-9]*$");

    private static ApiMessageRequirement Send(MessageKey key) =>
        new(key, MessageDirection.Out, ApiMessageRole.Send);

    private static ApiMessageRequirement Observe(MessageKey key) =>
        new(key, MessageDirection.In, ApiMessageRole.Observe);
}

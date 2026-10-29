using System.Diagnostics.CodeAnalysis;
using Qx.Game.Api;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Game.Rooms;

internal static class RoomReadsDescriptors
{
    public static ApiDescriptor DataGet { get; } = Read<RoomDataReadRequest, RoomDataReadResult>(
        ApiIds.RoomDataGet,
        "Room data",
        "Loads an immutable navigator snapshot for one room in the active hotel session.",
        MessageKeys.Room.SnapshotRequest,
        MessageKeys.Room.Snapshot);

    public static ApiDescriptor RightsList { get; } = Read<RoomRightsReadRequest, RoomRightsReadResult>(
        ApiIds.RoomRightsList,
        "Room rights",
        "Loads the immutable rights-holder list for one owned room in the active hotel session.",
        MessageKeys.Room.Authority.ControllersRequest,
        MessageKeys.Room.Authority.ControllersSnapshot);

    public static ApiDescriptor PetInfoGet { get; } = Read<PetInfoReadRequest, PetInfoReadResult>(
        ApiIds.PetsInfoGet,
        "Pet info",
        "Loads an immutable statistics snapshot for one pet in the active hotel session.",
        MessageKeys.Room.Occupants.Pet.InfoRequest,
        MessageKeys.Room.Occupants.Pet.Info,
        "pet_id",
        "Positive pet identifier.");

    public static ApiDescriptor StickyGet { get; } = Read<StickyReadRequest, StickyReadResult>(
        ApiIds.RoomStickyGet,
        "Sticky data",
        "Loads the immutable color and text of one sticky note in the active hotel session.",
        MessageKeys.Room.WallItem.StickyDataRequest,
        MessageKeys.Room.WallItem.StickyData,
        "item_id",
        "Positive sticky note item identifier.");

    public static ApiDescriptor RoomAdInfoGet { get; } = new(
        ApiIds.CatalogRoomAdInfoGet,
        "Room advertisement purchase info",
        "Loads the latest observed eligible-room list and membership flag from the active hotel session.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(RoomAdInfoReadRequest),
        typeof(RoomAdInfoReadResult),
        [
            new(
                "timeout_milliseconds",
                typeof(int),
                false,
                10000,
                "Maximum time for dispatch and the first fresh route response.",
                new(Minimum: 1, Maximum: 120000)),
            new(
                "expected_session_generation",
                typeof(long?),
                false,
                null,
                "Optional active hotel-session generation guard.",
                new(Minimum: 0))
        ],
        [GameRequirement.HotelConnected],
        messages:
        [
            new(MessageKeys.Catalog.RoomAdInfoRequest, MessageDirection.Out, ApiMessageRole.Send),
            new(MessageKeys.Catalog.RoomAdInfo, MessageDirection.In, ApiMessageRole.Observe)
        ],
        toolHints: new(true, false, true, true));

    private static ApiDescriptor Read<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRequest, TResult>(
        string id,
        string title,
        string description,
        MessageKey request_key,
        MessageKey snapshot_key,
        string id_name = "room_id",
        string id_description = "Positive room identifier.") => new(
            id,
            title,
            description,
            ApiMemberKind.Operation,
            ApiExposure.All,
            typeof(TRequest),
            typeof(TResult),
            [
                new(
                    id_name,
                    typeof(Id),
                    true,
                    null,
                    id_description,
                    new(Pattern: "^[1-9][0-9]*$")),
                new(
                    "timeout_milliseconds",
                    typeof(int),
                    false,
                    10000,
                    "Maximum time for dispatch and the matching hotel response.",
                    new(Minimum: 1, Maximum: 120000)),
                new(
                    "expected_session_generation",
                    typeof(long?),
                    false,
                    null,
                    "Optional active hotel-session generation guard.",
                    new(Minimum: 0))
            ],
            [GameRequirement.HotelConnected],
            messages:
            [
                new(request_key, MessageDirection.Out, ApiMessageRole.Send),
                new(snapshot_key, MessageDirection.In, ApiMessageRole.Observe)
            ],
            toolHints: new(true, false, true, true));
}

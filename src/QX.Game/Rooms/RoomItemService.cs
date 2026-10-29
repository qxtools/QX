using System.Diagnostics.CodeAnalysis;
using Qx.Game.Api;
using Qx.Interception;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Game.Rooms;

internal sealed class RoomItemService : IGameFeature
{
    private readonly IConnection connection;
    private readonly GameState game;
    private readonly TimeProvider time_provider;
    private int disposed;

    public RoomItemService(
        IInterceptor interceptor,
        GameState game,
        TimeProvider time_provider)
    {
        ArgumentNullException.ThrowIfNull(interceptor);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(time_provider);
        connection = interceptor;
        this.game = game;
        this.time_provider = time_provider;
        Bindings = Array.AsReadOnly<IApiBinding>(
        [
            Call<RoomFloorItemUseRequest>(FloorUseDescriptor(), FloorUse),
            Call<RoomWallItemUseRequest>(WallUseDescriptor(), WallUse),
            Call<RoomFloorItemClickRequest>(FloorClickDescriptor(), FloorClick),
            Call<RoomWallItemClickRequest>(WallClickDescriptor(), WallClick),
            Call<RoomOneWayDoorEnterRequest>(OneWayDoorDescriptor(), OneWayDoorEnter),
            Call<RoomDiceRequest>(DiceThrowDescriptor(), DiceThrow),
            Call<RoomDiceRequest>(DiceClearDescriptor(), DiceClear),
            Call<RoomWallItemRemoveRequest>(WallRemoveDescriptor(), WallRemove),
            Call<RoomStickySetRequest>(StickySetDescriptor(), StickySet),
            Call<RoomPostItPlaceRequest>(PostItPlaceDescriptor(), PostItPlace),
            Call<RoomPostItAddRequest>(PostItAddDescriptor(), PostItAdd)
        ]);
    }

    public IReadOnlyList<IApiBinding> Bindings { get; }

    public void Dispose() => Interlocked.Exchange(ref disposed, 1);

    private RoomItemDispatchResult FloorUse(
        RoomFloorItemUseRequest request,
        CancellationToken cancellation_token) => Dispatch(
            (session, generation, cancellation) => game.RoomActions.UseFloorItem(
                request.ItemId,
                request.State,
                session,
                generation,
                cancellation),
            cancellation_token);

    private RoomItemDispatchResult WallUse(
        RoomWallItemUseRequest request,
        CancellationToken cancellation_token) => Dispatch(
            (session, generation, cancellation) => game.RoomActions.UseWallItem(
                request.ItemId,
                request.State,
                session,
                generation,
                cancellation),
            cancellation_token);

    private RoomItemDispatchResult FloorClick(
        RoomFloorItemClickRequest request,
        CancellationToken cancellation_token) => Dispatch(
            (session, generation, cancellation) => game.RoomActions.ClickItem(
                request.ItemId,
                ItemType.Floor,
                session,
                generation,
                cancellation),
            cancellation_token);

    private RoomItemDispatchResult WallClick(
        RoomWallItemClickRequest request,
        CancellationToken cancellation_token) => Dispatch(
            (session, generation, cancellation) => game.RoomActions.ClickItem(
                request.ItemId,
                ItemType.Wall,
                session,
                generation,
                cancellation),
            cancellation_token);

    private RoomItemDispatchResult OneWayDoorEnter(
        RoomOneWayDoorEnterRequest request,
        CancellationToken cancellation_token) => Dispatch(
            (session, generation, cancellation) => game.RoomActions.EnterOneWayDoor(
                request.ItemId,
                session,
                generation,
                cancellation),
            cancellation_token);

    private RoomItemDispatchResult DiceThrow(
        RoomDiceRequest request,
        CancellationToken cancellation_token) => Dispatch(
            (session, generation, cancellation) => game.RoomActions.ThrowDice(
                request.ItemId,
                session,
                generation,
                cancellation),
            cancellation_token);

    private RoomItemDispatchResult DiceClear(
        RoomDiceRequest request,
        CancellationToken cancellation_token) => Dispatch(
            (session, generation, cancellation) => game.RoomActions.DiceOff(
                request.ItemId,
                session,
                generation,
                cancellation),
            cancellation_token);

    private RoomItemDispatchResult WallRemove(
        RoomWallItemRemoveRequest request,
        CancellationToken cancellation_token) => Dispatch(
            (session, generation, cancellation) => game.RoomActions.RemoveWallItem(
                request.ItemId,
                session,
                generation,
                cancellation),
            cancellation_token);

    private RoomItemDispatchResult StickySet(
        RoomStickySetRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request.Color);
        ArgumentNullException.ThrowIfNull(request.Text);
        return Dispatch(
            (session, generation, cancellation) => game.RoomActions.SetStickyData(
                request.ItemId,
                request.Color,
                request.Text,
                session,
                generation,
                cancellation),
            cancellation_token);
    }

    private RoomItemDispatchResult PostItPlace(
        RoomPostItPlaceRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request.WallLocation);
        return Dispatch(
            (session, generation, cancellation) => game.RoomActions.PlacePostIt(
                request.ItemId,
                request.WallLocation,
                session,
                generation,
                cancellation),
            cancellation_token);
    }

    private RoomItemDispatchResult PostItAdd(
        RoomPostItAddRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request.WallLocation);
        ArgumentNullException.ThrowIfNull(request.Color);
        ArgumentNullException.ThrowIfNull(request.Text);
        return Dispatch(
            (session, generation, cancellation) => game.RoomActions.AddSpamWallPostIt(
                request.ItemId,
                request.WallLocation,
                request.Color,
                request.Text,
                session,
                generation,
                cancellation),
            cancellation_token);
    }

    private RoomItemDispatchResult Dispatch(
        Action<Session, long, CancellationToken> send,
        CancellationToken cancellation_token)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        cancellation_token.ThrowIfCancellationRequested();
        Session session = connection.Session
            ?? throw new InvalidOperationException("An active hotel session is required.");
        var room = game.Room.Capture(state =>
        {
            Id? room_id = state.RoomId == 0 ? null : (Id)state.RoomId;
            return (RoomId: room_id, state.Generation);
        });
        send(session, room.Generation, cancellation_token);
        return new RoomItemDispatchResult(
            room.RoomId,
            room.Generation,
            true,
            false,
            time_provider.GetUtcNow());
    }

    private static ApiCall<TRequest, RoomItemDispatchResult> Call<TRequest>(
        ApiDescriptor descriptor,
        Func<TRequest, CancellationToken, RoomItemDispatchResult> invocation) => new(
            descriptor,
            (request, cancellation_token) =>
            {
                ArgumentNullException.ThrowIfNull(request);
                return ValueTask.FromResult(invocation(request, cancellation_token));
            });

    private static ApiDescriptor FloorUseDescriptor() => Descriptor<RoomFloorItemUseRequest>(
        ApiIds.RoomItemFloorUse,
        "Use floor item",
        "Uses an interaction state on a floor item in the current room.",
        [IdParameter(), StateParameter()],
        MessageKeys.Room.FloorItem.Use);

    private static ApiDescriptor WallUseDescriptor() => Descriptor<RoomWallItemUseRequest>(
        ApiIds.RoomItemWallUse,
        "Use wall item",
        "Uses an interaction state on a wall item in the current room.",
        [IdParameter(), StateParameter()],
        MessageKeys.Room.WallItem.Use);

    private static ApiDescriptor FloorClickDescriptor() => Descriptor<RoomFloorItemClickRequest>(
        ApiIds.RoomItemFloorClick,
        "Click floor item",
        "Clicks a floor item in the current room the way the client does, without using it.",
        [IdParameter()],
        MessageKeys.Room.Item.Click);

    private static ApiDescriptor WallClickDescriptor() => Descriptor<RoomWallItemClickRequest>(
        ApiIds.RoomItemWallClick,
        "Click wall item",
        "Clicks a wall item in the current room the way the client does, without using it.",
        [IdParameter()],
        MessageKeys.Room.Item.Click);

    private static ApiDescriptor OneWayDoorDescriptor() => Descriptor<RoomOneWayDoorEnterRequest>(
        ApiIds.RoomItemOneWayDoorEnter,
        "Enter one-way door",
        "Requests passage through a one-way door item.",
        [IdParameter()],
        MessageKeys.Room.FloorItem.OneWayDoorEnter);

    private static ApiDescriptor DiceThrowDescriptor() => Descriptor<RoomDiceRequest>(
        ApiIds.RoomItemDiceThrow,
        "Throw dice",
        "Throws a dice item in the current room.",
        [IdParameter()],
        MessageKeys.Room.FloorItem.ThrowDice);

    private static ApiDescriptor DiceClearDescriptor() => Descriptor<RoomDiceRequest>(
        ApiIds.RoomItemDiceClear,
        "Clear dice",
        "Clears a dice item to its blank state.",
        [IdParameter()],
        MessageKeys.Room.FloorItem.DiceOff);

    private static ApiDescriptor WallRemoveDescriptor() => Descriptor<RoomWallItemRemoveRequest>(
        ApiIds.RoomItemWallRemove,
        "Remove wall item",
        "Deletes a wall item from the current room.",
        [IdParameter()],
        MessageKeys.Room.WallItem.Remove);

    private static ApiDescriptor StickySetDescriptor() => Descriptor<RoomStickySetRequest>(
        ApiIds.RoomItemStickySet,
        "Set sticky data",
        "Replaces a sticky note color and text.",
        [
            IdParameter(),
            StringParameter(nameof(RoomStickySetRequest.Color).ToLowerInvariant()),
            StringParameter(nameof(RoomStickySetRequest.Text).ToLowerInvariant())
        ],
        MessageKeys.Room.WallItem.StickyDataSet);

    private static ApiDescriptor PostItPlaceDescriptor() => Descriptor<RoomPostItPlaceRequest>(
        ApiIds.RoomItemPostItPlace,
        "Place post-it",
        "Places an empty post-it on a room wall.",
        [IdParameter(), StringParameter("wall_location")],
        MessageKeys.Room.WallItem.PostItPlace);

    private static ApiDescriptor PostItAddDescriptor() => Descriptor<RoomPostItAddRequest>(
        ApiIds.RoomItemPostItAdd,
        "Add post-it",
        "Places a post-it with initial color and text on a room wall.",
        [
            IdParameter(),
            StringParameter("wall_location"),
            StringParameter(nameof(RoomPostItAddRequest.Color).ToLowerInvariant()),
            StringParameter(nameof(RoomPostItAddRequest.Text).ToLowerInvariant())
        ],
        MessageKeys.Room.WallItem.SpamPostItAdd);

    private static ApiDescriptor Descriptor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRequest>(
        string id,
        string title,
        string description,
        ApiParameter[] parameters,
        MessageKey message) => new(
            id,
            title,
            description,
            ApiMemberKind.Operation,
            ApiExposure.All,
            typeof(TRequest),
            typeof(RoomItemDispatchResult),
            parameters,
            [GameRequirement.HotelConnected],
            messages:
            [
                new ApiMessageRequirement(
                    message,
                    MessageDirection.Out,
                    ApiMessageRole.Send)
            ],
            toolHints: new(false, true, false, true));

    private static ApiParameter IdParameter() => new(
        "item_id",
        typeof(Id),
        true,
        null,
        "Room item identifier.");

    private static ApiParameter StateParameter() => new(
        "state",
        typeof(int),
        false,
        0,
        "Interaction state sent to Flash.");

    private static ApiParameter StringParameter(string name) => new(
        name,
        typeof(string),
        true,
        null,
        "String value sent to Flash.");
}

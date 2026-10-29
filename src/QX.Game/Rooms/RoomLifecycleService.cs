using Qx.Game.Api;
using Qx.Interception;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Game.Rooms;

internal sealed class RoomLifecycleService : IGameFeature
{
    private readonly IConnection connection;
    private readonly GameState game;
    private readonly TimeProvider time_provider;
    private int disposed;

    public RoomLifecycleService(
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
            new ApiCall<RoomEnterRequest, RoomLifecycleDispatchResult>(
                EnterDescriptor(),
                Enter),
            new ApiCall<RoomLeaveRequest, RoomLifecycleDispatchResult>(
                LeaveDescriptor(),
                Leave)
        ]);
    }

    public IReadOnlyList<IApiBinding> Bindings { get; }

    public void Dispose() => Interlocked.Exchange(ref disposed, 1);

    private ValueTask<RoomLifecycleDispatchResult> Enter(
        RoomEnterRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Password);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        cancellation_token.ThrowIfCancellationRequested();
        Session session = connection.Session
            ?? throw new InvalidOperationException("An active hotel session is required.");
        long generation = game.Room.Capture(state => state.Generation);
        game.RoomActions.Enter(
            request.RoomId,
            request.Password,
            request.EntryPoint,
            session,
            cancellation_token);
        return ValueTask.FromResult(new RoomLifecycleDispatchResult(
            request.RoomId,
            generation,
            true,
            false,
            time_provider.GetUtcNow()));
    }

    private ValueTask<RoomLifecycleDispatchResult> Leave(
        RoomLeaveRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        cancellation_token.ThrowIfCancellationRequested();
        Session session = connection.Session
            ?? throw new InvalidOperationException("An active hotel session is required.");
        var room = game.Room.Capture(state =>
        {
            Id? room_id = state.RoomId == 0 ? null : (Id)state.RoomId;
            return (RoomId: room_id, state.Generation);
        });
        game.RoomActions.Leave(session, room.Generation, cancellation_token);
        return ValueTask.FromResult(new RoomLifecycleDispatchResult(
            room.RoomId,
            room.Generation,
            true,
            false,
            time_provider.GetUtcNow()));
    }

    private static ApiDescriptor EnterDescriptor() => new(
        ApiIds.RoomEnter,
        "Enter room",
        "Requests entry into a room through the active hotel session.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(RoomEnterRequest),
        typeof(RoomLifecycleDispatchResult),
        [
            new("room_id", typeof(Id), true, null, "Target room identifier."),
            new("password", typeof(string), false, "", "Room password."),
            new("entry_point", typeof(long), false, -1L, "Navigator entry point identifier.")
        ],
        [GameRequirement.HotelConnected],
        messages:
        [
            new ApiMessageRequirement(
                MessageKeys.Room.Access.OpenRequest,
                MessageDirection.Out,
                ApiMessageRole.Send)
        ],
        toolHints: new(false, true, false, true));

    private static ApiDescriptor LeaveDescriptor() => new(
        ApiIds.RoomLeave,
        "Leave room",
        "Requests exit from the current room.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(RoomLeaveRequest),
        typeof(RoomLifecycleDispatchResult),
        [],
        [GameRequirement.HotelConnected],
        messages:
        [
            new ApiMessageRequirement(
                MessageKeys.Room.Lifecycle.Quit,
                MessageDirection.Out,
                ApiMessageRole.Send)
        ],
        toolHints: new(false, true, false, true));
}

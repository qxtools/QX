using System.Diagnostics.CodeAnalysis;
using Qx.Game.Api;
using Qx.Interception;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Game.Rooms;

internal interface IRoomAvatarOperations
{
    RoomAvatarDispatchResult Walk(
        RoomAvatarWalkRequest request,
        CancellationToken cancellation_token = default);

    RoomAvatarDispatchResult Look(
        RoomAvatarLookRequest request,
        CancellationToken cancellation_token = default);

    RoomAvatarDispatchResult Dance(
        RoomAvatarDanceRequest request,
        CancellationToken cancellation_token = default);

    RoomAvatarDispatchResult Expression(
        RoomAvatarExpressionRequest request,
        CancellationToken cancellation_token = default);

    RoomAvatarDispatchResult Posture(
        RoomAvatarPostureRequest request,
        CancellationToken cancellation_token = default);

    RoomAvatarDispatchResult Sign(
        RoomAvatarSignRequest request,
        CancellationToken cancellation_token = default);

    RoomAvatarDispatchResult Effect(
        RoomAvatarEffectRequest request,
        CancellationToken cancellation_token = default);

    RoomAvatarDispatchResult Typing(
        RoomAvatarTypingRequest request,
        CancellationToken cancellation_token = default);
}

internal sealed class RoomAvatarService : IGameFeature, IRoomAvatarOperations
{
    private readonly IConnection connection;
    private readonly GameState game;
    private readonly TimeProvider time_provider;
    private int disposed;

    public RoomAvatarService(
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
            Call<RoomAvatarWalkRequest>(WalkDescriptor(), Walk),
            Call<RoomAvatarLookRequest>(LookDescriptor(), Look),
            Call<RoomAvatarDanceRequest>(DanceDescriptor(), Dance),
            Call<RoomAvatarExpressionRequest>(ExpressionDescriptor(), Expression),
            Call<RoomAvatarPostureRequest>(PostureDescriptor(), Posture),
            Call<RoomAvatarSignRequest>(SignDescriptor(), Sign),
            Call<RoomAvatarEffectRequest>(EffectDescriptor(), Effect),
            Call<RoomAvatarTypingRequest>(TypingDescriptor(), Typing)
        ]);
        try
        {
            game.BindRoomAvatarOperations(this);
        }
        catch
        {
            Volatile.Write(ref disposed, 1);
            throw;
        }
    }

    public IReadOnlyList<IApiBinding> Bindings { get; }

    public RoomAvatarDispatchResult Walk(
        RoomAvatarWalkRequest request,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Dispatch(
            (session, generation, cancellation) => game.RoomActions.Walk(
                request.X,
                request.Y,
                session,
                generation,
                cancellation),
            cancellation_token);
    }

    public RoomAvatarDispatchResult Look(
        RoomAvatarLookRequest request,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Dispatch(
            (session, generation, cancellation) => game.RoomActions.LookTo(
                request.X,
                request.Y,
                session,
                generation,
                cancellation),
            cancellation_token);
    }

    public RoomAvatarDispatchResult Dance(
        RoomAvatarDanceRequest request,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Dispatch(
            (session, generation, cancellation) => game.RoomActions.Dance(
                request.Style,
                session,
                generation,
                cancellation),
            cancellation_token);
    }

    public RoomAvatarDispatchResult Expression(
        RoomAvatarExpressionRequest request,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Dispatch(
            (session, generation, cancellation) => game.RoomActions.Expression(
                request.Expression,
                session,
                generation,
                cancellation),
            cancellation_token);
    }

    public RoomAvatarDispatchResult Posture(
        RoomAvatarPostureRequest request,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Dispatch(
            (session, generation, cancellation) => game.RoomActions.SetPosture(
                request.Posture,
                session,
                generation,
                cancellation),
            cancellation_token);
    }

    public RoomAvatarDispatchResult Sign(
        RoomAvatarSignRequest request,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Dispatch(
            (session, generation, cancellation) => game.RoomActions.Sign(
                request.Sign,
                session,
                generation,
                cancellation),
            cancellation_token);
    }

    public RoomAvatarDispatchResult Effect(
        RoomAvatarEffectRequest request,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Dispatch(
            (session, generation, cancellation) => game.RoomActions.SelectEffect(
                request.Effect,
                session,
                generation,
                cancellation),
            cancellation_token);
    }

    public RoomAvatarDispatchResult Typing(
        RoomAvatarTypingRequest request,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Dispatch(
            (session, generation, cancellation) => game.RoomActions.SetTyping(
                request.Active,
                session,
                generation,
                cancellation),
            cancellation_token);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        game.UnbindRoomAvatarOperations(this);
    }

    private RoomAvatarDispatchResult Dispatch(
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
        return new RoomAvatarDispatchResult(
            room.RoomId,
            room.Generation,
            true,
            false,
            time_provider.GetUtcNow());
    }

    private static ApiCall<TRequest, RoomAvatarDispatchResult> Call<TRequest>(
        ApiDescriptor descriptor,
        Func<TRequest, CancellationToken, RoomAvatarDispatchResult> invocation) => new(
            descriptor,
            (request, cancellation_token) =>
                ValueTask.FromResult(invocation(request, cancellation_token)));

    private static ApiDescriptor WalkDescriptor() => Descriptor<RoomAvatarWalkRequest>(
        ApiIds.RoomAvatarWalk,
        "Walk",
        "Requests movement to a tile in the current room.",
        [
            Parameter(nameof(RoomAvatarWalkRequest.X).ToLowerInvariant()),
            Parameter(nameof(RoomAvatarWalkRequest.Y).ToLowerInvariant())
        ],
        [MessageKeys.Room.Movement.Walk]);

    private static ApiDescriptor LookDescriptor() => Descriptor<RoomAvatarLookRequest>(
        ApiIds.RoomAvatarLook,
        "Look toward tile",
        "Turns the local avatar toward a tile in the current room.",
        [
            Parameter(nameof(RoomAvatarLookRequest.X).ToLowerInvariant()),
            Parameter(nameof(RoomAvatarLookRequest.Y).ToLowerInvariant())
        ],
        [MessageKeys.Room.Movement.LookTo]);

    private static ApiDescriptor DanceDescriptor() => Descriptor<RoomAvatarDanceRequest>(
        ApiIds.RoomAvatarDance,
        "Avatar dance",
        "Sets the local avatar dance style.",
        [Parameter(nameof(RoomAvatarDanceRequest.Style).ToLowerInvariant())],
        [MessageKeys.Room.Occupants.Action.DanceRequest]);

    private static ApiDescriptor ExpressionDescriptor() =>
        Descriptor<RoomAvatarExpressionRequest>(
            ApiIds.RoomAvatarExpression,
            "Avatar expression",
            "Plays an expression on the local avatar.",
            [Parameter(nameof(RoomAvatarExpressionRequest.Expression).ToLowerInvariant())],
            [MessageKeys.Room.Occupants.Action.ExpressionRequest]);

    private static ApiDescriptor PostureDescriptor() =>
        Descriptor<RoomAvatarPostureRequest>(
            ApiIds.RoomAvatarPosture,
            "Avatar posture",
            "Sets the local avatar posture.",
            [Parameter(nameof(RoomAvatarPostureRequest.Posture).ToLowerInvariant())],
            [MessageKeys.Room.Occupants.Action.PostureRequest]);

    private static ApiDescriptor SignDescriptor() => Descriptor<RoomAvatarSignRequest>(
        ApiIds.RoomAvatarSign,
        "Avatar sign",
        "Raises a sign above the local avatar.",
        [Parameter(nameof(RoomAvatarSignRequest.Sign).ToLowerInvariant())],
        [MessageKeys.Room.Occupants.Action.SignRequest]);

    private static ApiDescriptor EffectDescriptor() => Descriptor<RoomAvatarEffectRequest>(
        ApiIds.RoomAvatarEffect,
        "Avatar effect",
        "Selects an effect for the local avatar.",
        [Parameter(nameof(RoomAvatarEffectRequest.Effect).ToLowerInvariant())],
        [MessageKeys.Room.Occupants.Action.EffectSelectionRequest]);

    private static ApiDescriptor TypingDescriptor() => Descriptor<RoomAvatarTypingRequest>(
        ApiIds.RoomAvatarTyping,
        "Typing indicator",
        "Shows or hides the local avatar typing indicator.",
        [new("active", typeof(bool), true, null, "Whether the indicator is visible.")],
        [MessageKeys.Room.Typing.Start, MessageKeys.Room.Typing.Cancel]);

    private static ApiDescriptor Descriptor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRequest>(
        string id,
        string title,
        string description,
        ApiParameter[] parameters,
        MessageKey[] messages) => new(
            id,
            title,
            description,
            ApiMemberKind.Operation,
            ApiExposure.All,
            typeof(TRequest),
            typeof(RoomAvatarDispatchResult),
            parameters,
            [GameRequirement.HotelConnected],
            messages: messages
                .Select(message => new ApiMessageRequirement(
                    message,
                    MessageDirection.Out,
                    ApiMessageRole.Send))
                .ToArray(),
            toolHints: new(false, true, false, true));

    private static ApiParameter Parameter(string name) =>
        new(name, typeof(int), true, null, "Integer value sent to Flash.");
}

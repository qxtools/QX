using System.Diagnostics.CodeAnalysis;
using Qx.Game.Api;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Game.Rooms;

internal static class RoomModerationDescriptors
{
    private static readonly ApiExposure event_exposure =
        ApiExposure.Ui | ApiExposure.Cli | ApiExposure.Scripting;

    public static ApiDescriptor State { get; } = new(
        ApiIds.RoomModerationState,
        "Room moderation state",
        "Reads one bounded page of the immutable session-bound ban list.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(RoomModerationStateRequest),
        typeof(RoomModerationStateView),
        PageParameters(),
        stateEffects:
        [new(GameRequirement.RoomBansLoaded, ApiStateChange.Reads)],
        messages: StateMessages(),
        toolHints: new(true, false, true, false),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor Refresh { get; } = new(
        ApiIds.RoomModerationRefresh,
        "Refresh room ban list",
        "Requests the current ready room's ban list and returns its first bounded page.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(RoomModerationRefreshRequest),
        typeof(RoomModerationStateView),
        [
            Limit(),
            Timeout(),
            SessionGeneration(),
            ExpectedRoomId(),
            RoomGeneration()
        ],
        [GameRequirement.HotelConnected, GameRequirement.RoomReady],
        [new(GameRequirement.RoomBansLoaded, ApiStateChange.Changes)],
        [
            Send(MessageKeys.Room.Moderation.BansRequest),
            Observe(MessageKeys.Room.Moderation.BansSnapshot)
        ],
        new(true, false, true, true));

    public static ApiDescriptor Mute { get; } = Action<RoomModerationMuteRequest>(
        ApiIds.RoomModerationMute,
        "Mute room user",
        "Dispatches a bounded mute duration for one current room user without assuming hotel acceptance.",
        [
            UserId(),
            new("minutes", typeof(int), true, null, "Mute duration in whole minutes; zero removes the mute.", new(Minimum: 0, Maximum: 1440)),
            .. TargetGuards()
        ],
        [Send(MessageKeys.Room.Moderation.Mute)]);

    public static ApiDescriptor Kick { get; } = Action<RoomModerationTargetRequest>(
        ApiIds.RoomModerationKick,
        "Kick room user",
        "Dispatches a kick for one current room user without assuming hotel acceptance.",
        [UserId(), .. TargetGuards()],
        [Send(MessageKeys.Room.Moderation.Kick)]);

    public static ApiDescriptor Ban { get; } = Action<RoomModerationBanRequest>(
        ApiIds.RoomModerationBan,
        "Ban room user",
        "Dispatches one verified room-ban duration for a current room user without assuming hotel acceptance.",
        [
            UserId(),
            new("length", typeof(BanLength), true, null, "Hour, day or permanent ban duration."),
            .. TargetGuards()
        ],
        [Send(MessageKeys.Room.Moderation.Ban)],
        ApiStateChange.Invalidates);

    public static ApiDescriptor Unban { get; } = new(
        ApiIds.RoomModerationUnban,
        "Unban room user",
        "Dispatches an unban for an explicit room and user without assuming hotel acceptance.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(RoomModerationUnbanRequest),
        typeof(RoomModerationDispatchResult),
        [
            UserId(),
            new("room_id", typeof(Id), true, null, "Positive room identifier.", IdConstraint()),
            SessionGeneration(),
            RoomGeneration(),
            new("expected_snapshot_revision", typeof(long?), false, null, "Optional active-room ban snapshot revision guard.", new(Minimum: 0))
        ],
        [GameRequirement.HotelConnected],
        [new(GameRequirement.RoomBansLoaded, ApiStateChange.Invalidates)],
        [Send(MessageKeys.Room.Moderation.Unban)],
        new(false, true, true, true));

    public static ApiDescriptor Bounce { get; } = Action<RoomModerationTargetRequest>(
        ApiIds.RoomModerationBounce,
        "Bounce room user",
        "Dispatches an hour ban followed by its unban for one pinned current room user.",
        [UserId(), .. TargetGuards()],
        [
            Send(MessageKeys.Room.Moderation.Ban),
            Send(MessageKeys.Room.Moderation.Unban)
        ],
        ApiStateChange.Invalidates);

    public static ApiDescriptor Changed { get; } = new(
        ApiIds.RoomModerationChanged,
        "Room moderation changed",
        "Publishes ordered bounded ban-list state summaries.",
        ApiMemberKind.Event,
        event_exposure,
        null,
        typeof(RoomModerationChanged),
        stateEffects:
        [new(GameRequirement.RoomBansLoaded, ApiStateChange.Changes)],
        messages: StateMessages());

    private static ApiDescriptor Action<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRequest>(
        string id,
        string title,
        string description,
        IReadOnlyList<ApiParameter> parameters,
        IReadOnlyList<ApiMessageRequirement> messages,
        ApiStateChange? ban_effect = null) => new(
        id,
        title,
        description,
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(TRequest),
        typeof(RoomModerationDispatchResult),
        parameters,
        [
            GameRequirement.HotelConnected,
            GameRequirement.RoomReady,
            GameRequirement.ProfileLoaded
        ],
        ban_effect is null
            ? [new(GameRequirement.RoomActive, ApiStateChange.Reads)]
            : [
                new(GameRequirement.RoomActive, ApiStateChange.Reads),
                new(GameRequirement.RoomBansLoaded, ban_effect.Value)
            ],
        messages,
        new(false, true, false, true));

    private static IReadOnlyList<ApiParameter> PageParameters() =>
    [
        new("offset", typeof(int), false, 0, "Zero-based offset within the current immutable snapshot.", new(Minimum: 0)),
        Limit(),
        new("snapshot_revision", typeof(long?), false, null, "Exact snapshot revision required for continuation pages.", new(Minimum: 0))
    ];

    private static IReadOnlyList<ApiParameter> TargetGuards() =>
    [
        SessionGeneration(),
        ExpectedRoomId(),
        RoomGeneration(),
        new("expected_user_index", typeof(int?), false, null, "Optional current room-index guard for the target identity.", new(Minimum: 0))
    ];

    private static ApiParameter UserId() => new(
        "user_id",
        typeof(Id),
        true,
        null,
        "Positive hotel user identifier.",
        IdConstraint());

    private static ApiParameter SessionGeneration() => new(
        "expected_session_generation",
        typeof(long?),
        false,
        null,
        "Optional session-generation guard from room.moderation.state or room.moderation.changed.",
        new(Minimum: 0));

    private static ApiParameter ExpectedRoomId() => new(
        "expected_room_id",
        typeof(Id?),
        false,
        null,
        "Optional active room identity guard.",
        IdConstraint());

    private static ApiParameter RoomGeneration() => new(
        "expected_room_generation",
        typeof(long?),
        false,
        null,
        "Optional ready-room generation guard.",
        new(Minimum: 0));

    private static ApiParameter Limit() => new(
        "limit",
        typeof(int),
        false,
        100,
        "Maximum bans returned by this page.",
        new(Minimum: 1, Maximum: 500));

    private static ApiParameter Timeout() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum time to wait for the correlated hotel response.",
        new(Minimum: 1, Maximum: 120000));

    private static IReadOnlyList<ApiMessageRequirement> StateMessages() =>
    [
        Observe(MessageKeys.Room.Moderation.BansSnapshot, false),
        Observe(MessageKeys.Room.Moderation.UserUnbanned, false)
    ];

    private static ApiMessageRequirement Send(MessageKey key) =>
        new(key, MessageDirection.Out, ApiMessageRole.Send);

    private static ApiMessageRequirement Observe(MessageKey key, bool required = true) =>
        new(key, MessageDirection.In, ApiMessageRole.Observe, required);

    private static ApiConstraints IdConstraint() =>
        new(Pattern: "^[1-9][0-9]*$");
}

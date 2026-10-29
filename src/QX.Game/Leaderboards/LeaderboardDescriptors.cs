using Qx.Game.Api;
using Qx.Protocol;

namespace Qx.Game.Leaderboards;

internal static class LeaderboardDescriptors
{
    private static readonly ApiExposure event_exposure =
        ApiExposure.Ui | ApiExposure.Cli | ApiExposure.Scripting;

    public static ApiDescriptor State { get; } = new(
        ApiIds.LeaderboardsState,
        "Leaderboard state",
        "Reads one route from a current or retained immutable leaderboard snapshot.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(LeaderboardStateRequest),
        typeof(LeaderboardStateView),
        [ScopeParameter(), WeeklyParameter(), SnapshotRevisionParameter(false)],
        stateEffects: [ReadEffect()],
        messages: ObservedMessages(),
        toolHints: QueryHints(),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor Entries { get; } = new(
        ApiIds.LeaderboardsEntriesList,
        "Leaderboard entries",
        "Reads one hotel-ordered entry page for a route from an immutable snapshot.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(LeaderboardEntryPageRequest),
        typeof(LeaderboardEntryPage),
        [ScopeParameter(), WeeklyParameter(), OffsetParameter(), LimitParameter(), SnapshotRevisionParameter(true)],
        stateEffects: [ReadEffect()],
        messages: ObservedMessages(),
        toolHints: QueryHints(),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor Refresh { get; } = new(
        ApiIds.LeaderboardsRefresh,
        "Refresh leaderboard",
        "Reloads one leaderboard from the server. Dispatches one route request after passive requests drain and returns the first fresh route response matching the game and week. Responses do not identify the requested rank or direction.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(LeaderboardRefreshRequest),
        typeof(LeaderboardRefreshResult),
        RefreshParameters(),
        [GameRequirement.HotelConnected],
        [ChangeEffect()],
        AllMessages(),
        new ApiToolHints(true, false, true, true));

    public static ApiDescriptor WeekOffsetSet { get; } = new(
        ApiIds.LeaderboardsWeekOffsetSet,
        "Set leaderboard week offset",
        "Sets the local weekly-board offset, clamped to the latest observed hotel maximum.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(LeaderboardWeekOffsetRequest),
        typeof(LeaderboardWeekOffsetResult),
        [new ApiParameter("offset", typeof(int), true, null, "Non-negative week offset.", new(Minimum: 0))],
        stateEffects: [ChangeEffect()],
        messages: OptionalObservedMessages(),
        toolHints: new ApiToolHints(false, false, true, false),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor Changed { get; } = new(
        ApiIds.LeaderboardsChanged,
        "Leaderboard changed",
        "Publishes bounded route snapshots, week-setting changes, and reset transitions.",
        ApiMemberKind.Event,
        event_exposure,
        null,
        typeof(LeaderboardChanged),
        stateEffects: [ChangeEffect()],
        messages: ObservedMessages(),
        invocationScope: ApiLifetime.Persistent);

    private static IReadOnlyList<ApiParameter> RefreshParameters() =>
    [
        new("game_type_id", typeof(int), true, null, "Game type identifier."),
        ScopeParameter(),
        WeeklyParameter(),
        new("start_rank", typeof(int), false, -1, "Requested rank anchor; the response does not echo it."),
        new("direction", typeof(int), false, 0, "Zero walks down and one walks up.", new(Minimum: 0, Maximum: 1)),
        LimitParameter(),
        new("timeout_milliseconds", typeof(int), false, 10000, "Total response budget.", new(Minimum: 1, Maximum: 120000)),
        new("expected_session_generation", typeof(long?), false, null, "Optional active session generation required through dispatch.", new(Minimum: 1))
    ];

    private static ApiParameter ScopeParameter() => new(
        "scope",
        typeof(LeaderboardScope),
        false,
        LeaderboardScope.Total,
        "Total, friends, or groups leaderboard.");

    private static ApiParameter WeeklyParameter() => new(
        "weekly",
        typeof(bool),
        false,
        false,
        "Whether to use the weekly route.");

    private static ApiParameter OffsetParameter() => new(
        "offset",
        typeof(int),
        false,
        0,
        "Zero-based entry offset in the immutable snapshot.",
        new(Minimum: 0));

    private static ApiParameter LimitParameter() => new(
        "limit",
        typeof(int),
        false,
        100,
        "Maximum entries returned.",
        new(Minimum: 1, Maximum: 500));

    private static ApiParameter SnapshotRevisionParameter(bool continuation) => new(
        "snapshot_revision",
        typeof(long?),
        false,
        null,
        continuation
            ? "Snapshot revision returned by the first read and required for continuation pages."
            : "Optional retained snapshot revision; omitted to capture the current state.",
        new(Minimum: 1));

    private static IReadOnlyList<ApiMessageRequirement> AllMessages() =>
    [
        Send(MessageKeys.Leaderboards.Total.Request),
        Observe(MessageKeys.Leaderboards.Total.Snapshot),
        Send(MessageKeys.Leaderboards.Friends.Request),
        Observe(MessageKeys.Leaderboards.Friends.Snapshot),
        Send(MessageKeys.Leaderboards.Groups.Request),
        Observe(MessageKeys.Leaderboards.Groups.Snapshot),
        Send(MessageKeys.Leaderboards.WeeklyTotal.Request),
        Observe(MessageKeys.Leaderboards.WeeklyTotal.Snapshot),
        Send(MessageKeys.Leaderboards.WeeklyFriends.Request),
        Observe(MessageKeys.Leaderboards.WeeklyFriends.Snapshot),
        Send(MessageKeys.Leaderboards.WeeklyGroups.Request),
        Observe(MessageKeys.Leaderboards.WeeklyGroups.Snapshot)
    ];

    private static IReadOnlyList<ApiMessageRequirement> ObservedMessages() =>
        AllMessages().Where(message => message.Role is ApiMessageRole.Observe).ToArray();

    private static IReadOnlyList<ApiMessageRequirement> OptionalObservedMessages() =>
        ObservedMessages()
            .Select(message => message with { Required = false })
            .ToArray();

    private static ApiMessageRequirement Send(MessageKey key) =>
        new(key, MessageDirection.Out, ApiMessageRole.Send);

    private static ApiMessageRequirement Observe(MessageKey key) =>
        new(key, MessageDirection.In, ApiMessageRole.Observe);

    private static ApiStateEffect ReadEffect() =>
        new(GameRequirement.Leaderboards, ApiStateChange.Reads);

    private static ApiStateEffect ChangeEffect() =>
        new(GameRequirement.Leaderboards, ApiStateChange.Changes);

    private static ApiToolHints QueryHints() => new(true, false, true, false);
}

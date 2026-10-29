using Qx.Game.Api;
using Qx.Protocol;

namespace Qx.Game.Tasks;

internal static class DailyTaskDescriptors
{
    private static readonly ApiExposure event_exposure =
        ApiExposure.Ui |
        ApiExposure.Cli |
        ApiExposure.Scripting;

    public static ApiDescriptor State { get; } = new(
        ApiIds.DailyTasksState,
        "Daily task state",
        "Reads one bounded state view from a current or retained immutable daily task snapshot.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(DailyTaskStateRequest),
        typeof(DailyTaskStateView),
        [SnapshotRevisionParameter(false)],
        stateEffects: [ReadEffect()],
        messages: ObservedMessages(),
        toolHints: QueryHints(),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor Entries { get; } = new(
        ApiIds.DailyTasksEntriesList,
        "Daily task page",
        "Reads one hotel-ordered daily task page from an immutable snapshot.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(DailyTaskPageRequest),
        typeof(DailyTaskPage),
        PageParameters(),
        stateEffects: [ReadEffect()],
        messages: ObservedMessages(),
        toolHints: QueryHints(),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor Refresh { get; } = new(
        ApiIds.DailyTasksRefresh,
        "Refresh daily tasks",
        "Reloads the daily task list from the server. Dispatches one trusted list request after earlier passive requests drain and returns the first full snapshot committed after dispatch. The response carries no request identifier.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(DailyTaskRefreshRequest),
        typeof(DailyTaskRefreshResult),
        RefreshParameters(),
        [GameRequirement.HotelConnected],
        [ChangeEffect()],
        [
            Send(MessageKeys.DailyTasks.Request),
            Observe(MessageKeys.DailyTasks.Snapshot)
        ],
        new ApiToolHints(true, false, true, true));

    public static ApiDescriptor Claim { get; } = new(
        ApiIds.DailyTasksClaim,
        "Claim daily task",
        "Dispatches exactly one claim using the protocol's 32-bit task identifier projection and returns at the send boundary without attributing a later task update.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(DailyTaskClaimActionRequest),
        typeof(DailyTaskClaimDispatchReceipt),
        ClaimParameters(),
        [GameRequirement.HotelConnected],
        [ChangeEffect()],
        [Send(MessageKeys.DailyTasks.Claim)],
        new ApiToolHints(false, true, false, true));

    public static ApiDescriptor Changed { get; } = new(
        ApiIds.DailyTasksChanged,
        "Daily tasks changed",
        "Publishes bounded passive list, addition, progress, completion, claim, and reset changes.",
        ApiMemberKind.Event,
        event_exposure,
        null,
        typeof(DailyTaskChanged),
        stateEffects: [ChangeEffect()],
        messages: ObservedMessages(),
        invocationScope: ApiLifetime.Persistent);

    private static IReadOnlyList<ApiParameter> PageParameters() =>
    [
        new(
            "offset",
            typeof(int),
            false,
            0,
            "Zero-based offset within the immutable daily task snapshot.",
            new(Minimum: 0)),
        LimitParameter(),
        SnapshotRevisionParameter(true)
    ];

    private static IReadOnlyList<ApiParameter> RefreshParameters() =>
    [
        LimitParameter(),
        TimeoutParameter(),
        SessionGenerationParameter()
    ];

    private static IReadOnlyList<ApiParameter> ClaimParameters() =>
    [
        new(
            "task_id",
            typeof(long),
            true,
            null,
            "Task identifier narrowed to the low signed 32 bits by the Flash wire composer."),
        SessionGenerationParameter()
    ];

    private static ApiParameter LimitParameter() => new(
        "limit",
        typeof(int),
        false,
        100,
        "Maximum tasks returned from the first page.",
        new(Minimum: 1, Maximum: 500));

    private static ApiParameter TimeoutParameter() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum wait for this request and its correlated full snapshot.",
        new(Minimum: 1, Maximum: 120000));

    private static ApiParameter SessionGenerationParameter() => new(
        "expected_session_generation",
        typeof(long?),
        false,
        null,
        "Optional active hotel-session generation required through dispatch.",
        new(Minimum: 1));

    private static ApiParameter SnapshotRevisionParameter(
        bool continuation) => new(
        "snapshot_revision",
        typeof(long?),
        false,
        null,
        continuation
            ? "Snapshot revision returned by the first daily task read and required for continuation pages."
            : "Optional retained snapshot revision; omitted to capture the current daily task state.",
        new(Minimum: 1));

    private static IReadOnlyList<ApiMessageRequirement> ObservedMessages() =>
    [
        Observe(MessageKeys.DailyTasks.Snapshot),
        Observe(MessageKeys.DailyTasks.Added),
        Observe(MessageKeys.DailyTasks.Updated)
    ];

    private static ApiMessageRequirement Send(MessageKey key) =>
        new(key, MessageDirection.Out, ApiMessageRole.Send);

    private static ApiMessageRequirement Observe(MessageKey key) =>
        new(key, MessageDirection.In, ApiMessageRole.Observe);

    private static ApiStateEffect ReadEffect() =>
        new(GameRequirement.DailyTasks, ApiStateChange.Reads);

    private static ApiStateEffect ChangeEffect() =>
        new(GameRequirement.DailyTasks, ApiStateChange.Changes);

    private static ApiToolHints QueryHints() => new(true, false, true, false);
}

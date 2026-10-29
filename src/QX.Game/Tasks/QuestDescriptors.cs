using Qx.Game.Api;
using Qx.Protocol;

namespace Qx.Game.Tasks;

internal static class QuestDescriptors
{
    private static readonly ApiExposure event_exposure =
        ApiExposure.Ui |
        ApiExposure.Cli |
        ApiExposure.Scripting;

    public static ApiDescriptor State { get; } = new(
        ApiIds.QuestsState,
        "Quest state",
        "Reads one bounded state view from a current or retained immutable quest snapshot.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(QuestStateRequest),
        typeof(QuestStateView),
        [SnapshotRevisionParameter(false)],
        stateEffects: [ReadEffect()],
        messages: ObservedMessages(),
        toolHints: QueryHints(),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor Entries { get; } = new(
        ApiIds.QuestsEntriesList,
        "Quest entry page",
        "Reads one hotel-ordered available, seasonal, or combined quest page from an immutable snapshot.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(QuestEntryPageRequest),
        typeof(QuestEntryPage),
        PageParameters(),
        stateEffects: [ReadEffect()],
        messages: ObservedMessages(),
        toolHints: QueryHints(),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor AvailableRefresh { get; } = new(
        ApiIds.QuestsAvailableRefresh,
        "Refresh available quests",
        "Reloads the available quests from the server. Shares one trusted available-quest request after earlier passive requests drain and returns the first correlated full snapshot; the response carries no request identifier.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(QuestAvailableRefreshRequest),
        typeof(QuestAvailableRefreshResult),
        ListRefreshParameters(),
        [GameRequirement.HotelConnected],
        [ChangeEffect()],
        [
            Send(MessageKeys.Quests.Request),
            Observe(MessageKeys.Quests.Snapshot)
        ],
        RefreshHints());

    public static ApiDescriptor SeasonalRefresh { get; } = new(
        ApiIds.QuestsSeasonalRefresh,
        "Refresh seasonal quests",
        "Reloads the seasonal quests from the server. Shares one trusted seasonal-quest request after earlier passive requests drain and returns the first correlated full snapshot; the response carries no request identifier.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(QuestSeasonalRefreshRequest),
        typeof(QuestSeasonalRefreshResult),
        ListRefreshParameters(),
        [GameRequirement.HotelConnected],
        [ChangeEffect()],
        [
            Send(MessageKeys.Quests.SeasonalRequest),
            Observe(MessageKeys.Quests.SeasonalSnapshot)
        ],
        RefreshHints());

    public static ApiDescriptor DailyRefresh { get; } = new(
        ApiIds.QuestsDailyRefresh,
        "Refresh daily quest",
        "Requests a daily quest from the server. Queues daily requests by their pool and index because the response carries no request identifier; identical requests share one flight.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(QuestDailyRefreshRequest),
        typeof(QuestDailyRefreshResult),
        DailyRefreshParameters(),
        [GameRequirement.HotelConnected],
        [ChangeEffect()],
        [
            Send(MessageKeys.Quests.DailyRequest),
            Observe(MessageKeys.Quests.Daily)
        ],
        RefreshHints());

    public static ApiDescriptor Accept { get; } = SelectionAction(
        ApiIds.QuestsAccept,
        "Accept quest",
        "Dispatches exactly one quest acceptance and returns at the send boundary.",
        MessageKeys.Quests.Accept);

    public static ApiDescriptor Activate { get; } = SelectionAction(
        ApiIds.QuestsActivate,
        "Activate quest",
        "Dispatches exactly one quest activation and returns at the send boundary.",
        MessageKeys.Quests.Activate);

    public static ApiDescriptor Reject { get; } = SelectionAction(
        ApiIds.QuestsReject,
        "Reject quest",
        "Dispatches exactly one quest rejection and returns at the send boundary.",
        MessageKeys.Quests.Reject);

    public static ApiDescriptor Cancel { get; } = EmptyAction(
        ApiIds.QuestsCancel,
        "Cancel quest",
        "Dispatches exactly one cancellation for the active quest and returns at the send boundary.",
        MessageKeys.Quests.Cancel,
        true);

    public static ApiDescriptor TrackerOpen { get; } = EmptyAction(
        ApiIds.QuestsTrackerOpen,
        "Open quest tracker",
        "Dispatches exactly one tracker-open notification and returns at the send boundary.",
        MessageKeys.Quests.TrackerOpen,
        false);

    public static ApiDescriptor FriendRequestComplete { get; } = EmptyAction(
        ApiIds.QuestsFriendRequestComplete,
        "Complete friend-request quest step",
        "Dispatches exactly one friend-request quest progress notification and returns at the send boundary.",
        MessageKeys.Quests.FriendRequestCompleted,
        true);

    public static ApiDescriptor Changed { get; } = new(
        ApiIds.QuestsChanged,
        "Quests changed",
        "Publishes bounded available, seasonal, current, completion, cancellation, daily, and reset changes.",
        ApiMemberKind.Event,
        event_exposure,
        null,
        typeof(QuestChanged),
        stateEffects: [ChangeEffect()],
        messages: ObservedMessages(),
        invocationScope: ApiLifetime.Persistent);

    private static ApiDescriptor SelectionAction(
        string id,
        string title,
        string description,
        MessageKey key) => new(
            id,
            title,
            description,
            ApiMemberKind.Operation,
            ApiExposure.All,
            typeof(QuestSelectionActionRequest),
            typeof(QuestSelectionDispatchReceipt),
            SelectionParameters(),
            [GameRequirement.HotelConnected],
            [ChangeEffect()],
            [Send(key)],
            ActionHints(true));

    private static ApiDescriptor EmptyAction(
        string id,
        string title,
        string description,
        MessageKey key,
        bool destructive) => new(
            id,
            title,
            description,
            ApiMemberKind.Operation,
            ApiExposure.All,
            typeof(QuestDispatchRequest),
            typeof(QuestDispatchReceipt),
            [SessionGenerationParameter()],
            [GameRequirement.HotelConnected],
            [ChangeEffect()],
            [Send(key)],
            ActionHints(destructive));

    private static IReadOnlyList<ApiParameter> PageParameters() =>
    [
        new(
            "collection",
            typeof(QuestCollection),
            false,
            QuestCollection.Available,
            "Available, seasonal, or combined hotel-order view."),
        new(
            "offset",
            typeof(int),
            false,
            0,
            "Zero-based offset within the immutable quest collection.",
            new(Minimum: 0)),
        LimitParameter(),
        SnapshotRevisionParameter(true)
    ];

    private static IReadOnlyList<ApiParameter> ListRefreshParameters() =>
    [
        LimitParameter(),
        TimeoutParameter(),
        SessionGenerationParameter()
    ];

    private static IReadOnlyList<ApiParameter> DailyRefreshParameters() =>
    [
        new(
            "is_easy",
            typeof(bool),
            true,
            null,
            "Whether the daily quest is requested from the easy pool."),
        new(
            "index",
            typeof(int),
            true,
            null,
            "Hotel-defined index within the selected daily quest pool."),
        TimeoutParameter(),
        SessionGenerationParameter()
    ];

    private static IReadOnlyList<ApiParameter> SelectionParameters() =>
    [
        new(
            "quest_id",
            typeof(long),
            true,
            null,
            "Quest identifier, sent as a 32-bit integer."),
        SessionGenerationParameter()
    ];

    private static ApiParameter LimitParameter() => new(
        "limit",
        typeof(int),
        false,
        100,
        "Maximum entries returned from the first page.",
        new(Minimum: 1, Maximum: 500));

    private static ApiParameter TimeoutParameter() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum wait for this caller; timing out does not cancel an already dispatched request.",
        new(Minimum: 1, Maximum: 120000));

    private static ApiParameter SessionGenerationParameter() => new(
        "expected_session_generation",
        typeof(long?),
        false,
        null,
        "Optional active hotel-session generation required through dispatch and response.",
        new(Minimum: 1));

    private static ApiParameter SnapshotRevisionParameter(
        bool continuation) => new(
        "snapshot_revision",
        typeof(long?),
        false,
        null,
        continuation
            ? "Snapshot revision returned by the first quest read and required for continuation pages."
            : "Optional retained snapshot revision; omitted to capture the current quest state.",
        new(Minimum: 1));

    private static IReadOnlyList<ApiMessageRequirement> ObservedMessages() =>
    [
        Observe(MessageKeys.Quests.Snapshot),
        Observe(MessageKeys.Quests.SeasonalSnapshot),
        Observe(MessageKeys.Quests.Updated),
        Observe(MessageKeys.Quests.Completed),
        Observe(MessageKeys.Quests.Cancelled),
        Observe(MessageKeys.Quests.Daily)
    ];

    private static ApiMessageRequirement Send(MessageKey key) =>
        new(key, MessageDirection.Out, ApiMessageRole.Send);

    private static ApiMessageRequirement Observe(MessageKey key) =>
        new(key, MessageDirection.In, ApiMessageRole.Observe);

    private static ApiStateEffect ReadEffect() =>
        new(GameRequirement.Quests, ApiStateChange.Reads);

    private static ApiStateEffect ChangeEffect() =>
        new(GameRequirement.Quests, ApiStateChange.Changes);

    private static ApiToolHints QueryHints() => new(true, false, true, false);

    private static ApiToolHints RefreshHints() => new(true, false, true, true);

    private static ApiToolHints ActionHints(bool destructive) =>
        new(false, destructive, false, true);
}

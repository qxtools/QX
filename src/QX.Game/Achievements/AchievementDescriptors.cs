using Qx.Game.Api;
using Qx.Game.Messages;
using Qx.Protocol;

namespace Qx.Game.Achievements;

internal static class AchievementDescriptors
{
    private static readonly ApiExposure event_exposure =
        ApiExposure.Ui |
        ApiExposure.Cli |
        ApiExposure.Scripting;

    public static ApiDescriptor AchievementState { get; } = new(
        ApiIds.AchievementsState,
        "Achievement state",
        "Reads one bounded state view from a current or retained immutable achievement snapshot.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(AchievementStateRequest),
        typeof(AchievementStateView),
        [SnapshotRevisionParameter(false)],
        stateEffects: [AchievementRead()],
        messages: AchievementObservedMessages(),
        toolHints: QueryHints(),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor AchievementList { get; } = new(
        ApiIds.AchievementsList,
        "Achievement page",
        "Reads one hotel-ordered achievement page from an immutable achievement-domain snapshot.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(AchievementPageRequest),
        typeof(AchievementPage),
        PageParameters(),
        stateEffects: [AchievementRead()],
        messages:
        [
            Observe(MessageKeys.Achievements.Snapshot),
            Observe(MessageKeys.Achievements.Updated)
        ],
        toolHints: QueryHints(),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor AchievementPointLimitsList { get; } = new(
        ApiIds.AchievementPointLimitsList,
        "Achievement point-limit page",
        "Reads one globally ordered point-limit page from the same immutable achievement-domain snapshot used by state and achievement pages.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(AchievementPointLimitPageRequest),
        typeof(AchievementPointLimitPage),
        PageParameters(),
        stateEffects: [AchievementRead()],
        messages:
        [
            Observe(MessageKeys.Achievements.PointLimits)
        ],
        toolHints: QueryHints(),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor AchievementRefresh { get; } = new(
        ApiIds.AchievementsRefresh,
        "Refresh achievements",
        "Reloads the achievement list from the server. Coalesces callers within one hotel session and returns only a fresh full achievement snapshot for the dispatched request epoch; achievement updates never establish the baseline. Caller cancellation and timeout detach only that waiter.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(AchievementRefreshRequest),
        typeof(AchievementRefreshResult),
        RefreshParameters(),
        [GameRequirement.HotelConnected],
        [AchievementChange()],
        [
            Send(MessageKeys.Achievements.Request),
            Observe(MessageKeys.Achievements.Snapshot)
        ],
        RefreshHints());

    public static ApiDescriptor AchievementPointLimitsRefresh { get; } = new(
        ApiIds.AchievementPointLimitsRefresh,
        "Refresh achievement point limits",
        "Reloads the badge point limits from the server. Coalesces callers within one hotel session and returns only a fresh point-limit response for the dispatched request epoch. Caller cancellation and timeout detach only that waiter.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(AchievementPointLimitsRefreshRequest),
        typeof(AchievementPointLimitsRefreshResult),
        RefreshParameters(),
        [GameRequirement.HotelConnected],
        [AchievementChange()],
        [
            Send(MessageKeys.Achievements.PointLimitsRequest),
            Observe(MessageKeys.Achievements.PointLimits)
        ],
        RefreshHints());

    public static ApiDescriptor AchievementChanged { get; } = new(
        ApiIds.AchievementsChanged,
        "Achievements changed",
        "Publishes bounded passive achievement-domain changes, including full snapshots, incremental updates, score data, point limits, game-data new codes, and resets.",
        ApiMemberKind.Event,
        event_exposure,
        null,
        typeof(AchievementChanged),
        stateEffects: [AchievementChange()],
        messages: AchievementObservedMessages(),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor BadgeState { get; } = new(
        ApiIds.BadgesState,
        "Badge state",
        "Reads one bounded state view from a current or retained immutable badge snapshot.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(BadgeStateRequest),
        typeof(BadgeStateView),
        [SnapshotRevisionParameter(false)],
        stateEffects: [BadgeRead()],
        messages: BadgeObservedMessages(),
        toolHints: QueryHints(),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor OwnedBadgeList { get; } = new(
        ApiIds.BadgesOwnedList,
        "Owned badge page",
        "Reads one fragment- and insertion-ordered owned-badge page from an immutable badge-domain snapshot while preserving each badge's rarity-data presence.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(OwnedBadgePageRequest),
        typeof(OwnedBadgePage),
        PageParameters(),
        stateEffects: [BadgeRead()],
        messages:
        [
            Observe(MessageKeys.Badges.Snapshot),
            Observe(MessageKeys.Badges.Received),
            Observe(MessageKeys.Achievements.Notification)
        ],
        toolHints: QueryHints(),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor BadgeSelectedSetsList { get; } = new(
        ApiIds.BadgesSelectedSetsList,
        "Selected badge-set page",
        "Reads one user-id ordered page of retained selected-badge sets from an immutable badge-domain snapshot.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(BadgeSelectedSetPageRequest),
        typeof(BadgeSelectedSetPage),
        PageParameters(),
        stateEffects: [BadgeRead()],
        messages:
        [
            Observe(MessageKeys.Badges.Selected)
        ],
        toolHints: QueryHints(),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor BadgeSelectedList { get; } = new(
        ApiIds.BadgesSelectedList,
        "Selected badge page",
        "Reads one wire-ordered selected-badge page for a retained user from the same immutable badge-domain snapshot used by owned badges and selected sets.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(BadgeSelectedPageRequest),
        typeof(BadgeSelectedPage),
        [UserIdParameter(), .. PageParameters()],
        stateEffects: [BadgeRead()],
        messages:
        [
            Observe(MessageKeys.Badges.Selected)
        ],
        toolHints: QueryHints(),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor BadgeRefresh { get; } = new(
        ApiIds.BadgesRefresh,
        "Refresh owned badges",
        "Reloads the badge inventory from the server. Coalesces callers within one hotel session and publishes an owned-badge baseline only after every out-of-order fragment is present. Caller cancellation and timeout detach only that waiter; a response-free lane can retire after thirty seconds and surfaces ambiguous retired-baseline correlation explicitly.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(BadgeRefreshRequest),
        typeof(BadgeRefreshResult),
        RefreshParameters(),
        [GameRequirement.HotelConnected],
        [BadgeChange()],
        [
            Send(MessageKeys.Badges.Request),
            Observe(MessageKeys.Badges.Snapshot)
        ],
        RefreshHints());

    public static ApiDescriptor BadgeChanged { get; } = new(
        ApiIds.BadgesChanged,
        "Badges changed",
        "Publishes bounded badge loading, atomic baseline, live ownership, selected-set, correlation-failure, and reset changes.",
        ApiMemberKind.Event,
        event_exposure,
        null,
        typeof(BadgeChanged),
        stateEffects: [BadgeChange()],
        messages: BadgeObservedMessages(),
        invocationScope: ApiLifetime.Persistent);

    private static IReadOnlyList<ApiParameter> PageParameters() =>
    [
        new(
            "offset",
            typeof(int),
            false,
            0,
            "Zero-based offset within the immutable domain snapshot.",
            new(Minimum: 0)),
        LimitParameter(),
        SnapshotRevisionParameter(true)
    ];

    private static IReadOnlyList<ApiParameter> RefreshParameters() =>
    [
        LimitParameter(),
        new(
            "timeout_milliseconds",
            typeof(int),
            false,
            10000,
            "Maximum wait for this caller; timing out does not cancel the shared session request.",
            new(Minimum: 1, Maximum: 120000)),
        new(
            "expected_session_generation",
            typeof(long?),
            false,
            null,
            "Optional active hotel-session generation required through dispatch and response.",
            new(Minimum: 1))
    ];

    private static ApiParameter LimitParameter() => new(
        "limit",
        typeof(int),
        false,
        100,
        "Maximum rows returned from one collection.",
        new(Minimum: 1, Maximum: 500));

    private static ApiParameter SnapshotRevisionParameter(
        bool continuation) => new(
        "snapshot_revision",
        typeof(long?),
        false,
        null,
        continuation
            ? "Snapshot revision returned by the first domain read and required for continuation pages."
            : "Optional retained snapshot revision; omitted to capture the current domain state.",
        new(Minimum: 1));

    private static ApiParameter UserIdParameter() => new(
        "user_id",
        typeof(Id),
        true,
        null,
        "User identifier whose retained selected-badge set is read.",
        new(Pattern: "^-?[0-9]+$"));

    private static IReadOnlyList<ApiMessageRequirement>
        AchievementObservedMessages() =>
    [
        Observe(MessageKeys.Achievements.Snapshot),
        Observe(MessageKeys.Achievements.Updated),
        Observe(MessageKeys.Achievements.Score, false),
        Observe(MessageKeys.Achievements.PointLimits)
    ];

    private static IReadOnlyList<ApiMessageRequirement> BadgeObservedMessages() =>
    [
        Observe(MessageKeys.Badges.Snapshot),
        Observe(MessageKeys.Badges.Received),
        Observe(MessageKeys.Badges.Selected),
        Observe(MessageKeys.Achievements.Notification)
    ];

    private static ApiMessageRequirement Send(MessageKey key) =>
        new(key, MessageDirection.Out, ApiMessageRole.Send);

    private static ApiMessageRequirement Observe(
        MessageKey key,
        bool required = true) =>
        new(key, MessageDirection.In, ApiMessageRole.Observe, required);

    private static ApiStateEffect AchievementRead() =>
        new(GameRequirement.Achievements, ApiStateChange.Reads);

    private static ApiStateEffect AchievementChange() =>
        new(GameRequirement.Achievements, ApiStateChange.Changes);

    private static ApiStateEffect BadgeRead() =>
        new(GameRequirement.BadgeInventory, ApiStateChange.Reads);

    private static ApiStateEffect BadgeChange() =>
        new(GameRequirement.BadgeInventory, ApiStateChange.Changes);

    private static ApiToolHints QueryHints() => new(true, false, true, false);
    private static ApiToolHints RefreshHints() => new(true, false, true, true);
}

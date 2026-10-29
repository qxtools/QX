using Qx.Game.Api;
using Qx.Protocol;

namespace Qx.Game.Wallet;

internal static class EarningDescriptors
{
    private static readonly ApiExposure event_exposure =
        ApiExposure.Ui |
        ApiExposure.Cli |
        ApiExposure.Scripting;

    public static ApiDescriptor State { get; } = new(
        ApiIds.EarningsState,
        "Earning state",
        "Reads one bounded state view from a current or retained immutable earning snapshot.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(EarningStateRequest),
        typeof(EarningStateView),
        [SnapshotRevisionParameter(false)],
        stateEffects: [ReadEffect()],
        messages: ObservedMessages(),
        toolHints: QueryHints(),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor Entries { get; } = new(
        ApiIds.EarningsEntriesList,
        "Earning entry page",
        "Reads one hotel-ordered earning page from an immutable earning snapshot.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(EarningEntryPageRequest),
        typeof(EarningEntryPage),
        PageParameters(),
        stateEffects: [ReadEffect()],
        messages:
        [
            Observe(MessageKeys.Earnings.StatusSnapshot),
            Observe(MessageKeys.Earnings.Claimed)
        ],
        toolHints: QueryHints(),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor Refresh { get; } = new(
        ApiIds.EarningsRefresh,
        "Refresh earnings",
        "Reloads the earnings vault from the server. Shares one trusted status request within a hotel session after earlier passive requests drain and returns only its correlated full snapshot. Caller cancellation and timeout detach only that waiter.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(EarningRefreshRequest),
        typeof(EarningRefreshResult),
        RefreshParameters(),
        [GameRequirement.HotelConnected],
        [ChangeEffect()],
        [
            Send(MessageKeys.Earnings.StatusRequest),
            Observe(MessageKeys.Earnings.StatusSnapshot)
        ],
        new ApiToolHints(true, false, true, true));

    public static ApiDescriptor Claim { get; } = new(
        ApiIds.EarningsClaim,
        "Claim earnings",
        "Dispatches one claim for a signed wire category after earlier claims for that category drain. Same-category calls remain FIFO and are never coalesced.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(EarningClaimActionRequest),
        typeof(EarningClaimActionResult),
        ClaimParameters(),
        [GameRequirement.HotelConnected],
        [ChangeEffect()],
        [
            Send(MessageKeys.Earnings.Claim),
            Observe(MessageKeys.Earnings.Claimed)
        ],
        new ApiToolHints(false, true, false, true));

    public static ApiDescriptor Changed { get; } = new(
        ApiIds.EarningsChanged,
        "Earnings changed",
        "Publishes bounded passive earning snapshots, claim results, reward notifications, and resets.",
        ApiMemberKind.Event,
        event_exposure,
        null,
        typeof(EarningChanged),
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
            "Zero-based offset within the immutable earning snapshot.",
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
            "category",
            typeof(int),
            true,
            null,
            "Signed-byte earning category, including unknown future values and -1 for claim-all.",
            new(Minimum: sbyte.MinValue, Maximum: sbyte.MaxValue)),
        TimeoutParameter(),
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
            ? "Snapshot revision returned by the first earning read and required for continuation pages."
            : "Optional retained snapshot revision; omitted to capture the current earning state.",
        new(Minimum: 1));

    private static IReadOnlyList<ApiMessageRequirement> ObservedMessages() =>
    [
        Observe(MessageKeys.Earnings.StatusSnapshot),
        Observe(MessageKeys.Earnings.Claimed),
        Observe(MessageKeys.Earnings.Notification, false)
    ];

    private static ApiMessageRequirement Send(MessageKey key) =>
        new(key, MessageDirection.Out, ApiMessageRole.Send);

    private static ApiMessageRequirement Observe(
        MessageKey key,
        bool required = true) =>
        new(key, MessageDirection.In, ApiMessageRole.Observe, required);

    private static ApiStateEffect ReadEffect() =>
        new(GameRequirement.Earnings, ApiStateChange.Reads);

    private static ApiStateEffect ChangeEffect() =>
        new(GameRequirement.Earnings, ApiStateChange.Changes);

    private static ApiToolHints QueryHints() => new(true, false, true, false);
}

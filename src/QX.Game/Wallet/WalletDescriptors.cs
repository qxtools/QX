using Qx.Game.Api;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Game.Wallet;

internal static class WalletDescriptors
{
    private static readonly ApiExposure event_exposure =
        ApiExposure.Ui | ApiExposure.Cli | ApiExposure.Scripting;

    public static ApiDescriptor State { get; } = new(
        ApiIds.WalletState,
        "Wallet state",
        "Reads credits and a bounded deterministic activity-point page for the active session.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(WalletStateRequest),
        typeof(WalletStateView),
        StateParameters(),
        stateEffects:
        [new(GameRequirement.WalletLoaded, ApiStateChange.Reads)],
        messages: ObservedMessages(),
        toolHints: new(true, false, true, false),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor Refresh { get; } = new(
        ApiIds.WalletRefresh,
        "Refresh wallet",
        "Refreshes credits and returns the latest observed activity-point balances for the active session.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(WalletRefreshRequest),
        typeof(WalletStateView),
        [PointLimitParameter(), TimeoutParameter()],
        [GameRequirement.HotelConnected],
        [new(GameRequirement.WalletLoaded, ApiStateChange.Changes)],
        [
            new(MessageKeys.Wallet.CreditsRequest, MessageDirection.Out, ApiMessageRole.Send),
            new(MessageKeys.Wallet.CreditsBalance, MessageDirection.In, ApiMessageRole.Observe),
            new(MessageKeys.Wallet.ActivityPoints, MessageDirection.In, ApiMessageRole.Observe),
            new(
                MessageKeys.Wallet.ActivityPointUpdated,
                MessageDirection.In,
                ApiMessageRole.Observe,
                false)
        ],
        new(true, false, true, true));

    public static ApiDescriptor Changed { get; } = new(
        ApiIds.WalletChanged,
        "Wallet changed",
        "Publishes bounded credit, activity-point, delta and reset envelopes.",
        ApiMemberKind.Event,
        event_exposure,
        null,
        typeof(WalletChanged),
        stateEffects:
        [new(GameRequirement.WalletLoaded, ApiStateChange.Changes)],
        messages: ObservedMessages());

    private static IReadOnlyList<ApiParameter> StateParameters() =>
    [
        new(
            "point_offset",
            typeof(int),
            false,
            0,
            "Zero-based offset within the selected activity-point snapshot.",
            new(Minimum: 0)),
        PointLimitParameter(),
        new(
            "snapshot_revision",
            typeof(long?),
            false,
            null,
            "Activity-point snapshot revision required for continuation pages.",
            new(Minimum: 1)),
        new(
            "point_type",
            typeof(int?),
            false,
            null,
            "Optional exact activity-point type.")
    ];

    private static IReadOnlyList<ApiMessageRequirement> ObservedMessages() =>
    [
        new(MessageKeys.Wallet.CreditsBalance, MessageDirection.In, ApiMessageRole.Observe),
        new(MessageKeys.Wallet.ActivityPoints, MessageDirection.In, ApiMessageRole.Observe),
        new(MessageKeys.Wallet.ActivityPointUpdated, MessageDirection.In, ApiMessageRole.Observe)
    ];

    private static ApiParameter PointLimitParameter() => new(
        "point_limit",
        typeof(int),
        false,
        100,
        "Maximum activity-point balances returned by this page.",
        new(Minimum: 1, Maximum: 500));

    private static ApiParameter TimeoutParameter() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum total time for the credit balance response.",
        new(Minimum: 1, Maximum: 120000));
}

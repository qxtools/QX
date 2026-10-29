using System.Diagnostics.CodeAnalysis;
using Qx.Game.Api;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Game.Polls;

internal static class PollDescriptors
{
    private static readonly ApiExposure event_exposure =
        ApiExposure.Ui | ApiExposure.Cli | ApiExposure.Scripting;

    public static ApiDescriptor State { get; } = new(
        ApiIds.PollsState,
        "Poll state",
        "Reads the latest immutable poll offer, contents and request-error state for the active hotel session.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(PollStateRequest),
        typeof(PollStateView),
        stateEffects: [],
        messages: ObservedMessages(),
        toolHints: new(true, false, true, false),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor Start { get; } = Dispatch(
        ApiIds.PollsStart,
        "Start poll",
        "Dispatches a poll-start request without assuming that contents will arrive.",
        typeof(PollStartRequest),
        MessageKeys.Polls.Start,
        false);

    public static ApiDescriptor ContentsGet { get; } = new(
        ApiIds.PollsContentsGet,
        "Get poll contents",
        "Dispatches one poll-start request and waits for later contents with the same poll identifier.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(PollContentsGetRequest),
        typeof(PollStateView),
        [
            PollId(),
            Timeout(),
            SessionGeneration()
        ],
        [GameRequirement.HotelConnected],
        messages:
        [
            Send(MessageKeys.Polls.Start),
            Observe(MessageKeys.Polls.Contents),
            new(
                MessageKeys.Polls.Error,
                MessageDirection.In,
                ApiMessageRole.Observe,
                false)
        ],
        toolHints: new(false, false, false, true));

    public static ApiDescriptor Reject { get; } = Dispatch(
        ApiIds.PollsReject,
        "Reject poll",
        "Dispatches a poll rejection without assuming a server acknowledgement.",
        typeof(PollRejectRequest),
        MessageKeys.Polls.Reject,
        true);

    public static ApiDescriptor Answer { get; } = new(
        ApiIds.PollsAnswer,
        "Answer poll",
        "Dispatches bounded poll responses without assuming a server acknowledgement.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(PollAnswerRequest),
        typeof(PollDispatchReceipt),
        [
            PollId(),
            new(
                "responses",
                typeof(IReadOnlyList<PollResponseInput>),
                true,
                null,
                "Bounded question responses.",
                new(MinItems: 0, MaxItems: 500)),
            SessionGeneration()
        ],
        [GameRequirement.HotelConnected],
        messages: [Send(MessageKeys.Polls.Answer)],
        toolHints: new(false, false, false, true));

    public static ApiDescriptor Changed { get; } = new(
        ApiIds.PollsChanged,
        "Poll changed",
        "Publishes immutable offer, contents, error and session-reset state changes.",
        ApiMemberKind.Event,
        event_exposure,
        null,
        typeof(PollChanged),
        messages: ObservedMessages(),
        invocationScope: ApiLifetime.Persistent);

    private static ApiDescriptor Dispatch(
        string id,
        string title,
        string description,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type request_type,
        MessageKey key,
        bool destructive) => new(
        id,
        title,
        description,
        ApiMemberKind.Operation,
        ApiExposure.All,
        request_type,
        typeof(PollDispatchReceipt),
        [PollId(), SessionGeneration()],
        [GameRequirement.HotelConnected],
        messages: [Send(key)],
        toolHints: new(false, destructive, false, true));

    private static ApiParameter PollId() => new(
        "poll_id",
        typeof(Id),
        true,
        null,
        "Positive poll identifier.",
        new(Pattern: "^[1-9][0-9]*$"));

    private static ApiParameter Timeout() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum time to wait for matching poll contents.",
        new(Minimum: 1, Maximum: 120000));

    private static ApiParameter SessionGeneration() => new(
        "expected_session_generation",
        typeof(long?),
        false,
        null,
        "Optional active hotel-session generation guard.",
        new(Minimum: 0));

    private static ApiMessageRequirement Send(MessageKey key) =>
        new(key, MessageDirection.Out, ApiMessageRole.Send);

    private static ApiMessageRequirement Observe(MessageKey key) =>
        new(key, MessageDirection.In, ApiMessageRole.Observe);

    private static IReadOnlyList<ApiMessageRequirement> ObservedMessages() =>
    [
        Observe(MessageKeys.Polls.Offer),
        Observe(MessageKeys.Polls.Contents),
        Observe(MessageKeys.Polls.Error)
    ];
}

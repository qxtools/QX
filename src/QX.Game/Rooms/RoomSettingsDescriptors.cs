using Qx.Game.Api;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Game.Rooms;

internal static class RoomSettingsDescriptors
{
    private static readonly ApiExposure event_exposure =
        ApiExposure.Ui | ApiExposure.Cli | ApiExposure.Scripting;

    public static ApiDescriptor State { get; } = new(
        ApiIds.RoomSettingsState,
        "Room settings state",
        "Reads the bounded cached settings snapshot for one owned room.",
        ApiMemberKind.Query,
        ApiExposure.All,
        typeof(RoomSettingsStateRequest),
        typeof(RoomSettingsStateView),
        [RoomIdParameter()],
        stateEffects:
        [new(GameRequirement.RoomSettingsLoaded, ApiStateChange.Reads)],
        messages: ObservedMessages(),
        toolHints: new(true, false, true, false),
        invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor Get { get; } = new(
        ApiIds.RoomSettingsGet,
        "Get room settings",
        "Loads the complete editable settings and read-only metadata for one owned room.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(RoomSettingsGetRequest),
        typeof(RoomSettingsStateView),
        [
            RoomIdParameter(),
            TimeoutParameter(),
            SessionGenerationParameter(),
            RoomGenerationParameter()
        ],
        [GameRequirement.HotelConnected],
        [new(GameRequirement.RoomSettingsLoaded, ApiStateChange.Changes)],
        [
            new(MessageKeys.Room.Settings.Request, MessageDirection.Out, ApiMessageRole.Send),
            new(MessageKeys.Room.Settings.Snapshot, MessageDirection.In, ApiMessageRole.Observe),
            new(MessageKeys.Room.Settings.RequestFailed, MessageDirection.In, ApiMessageRole.Observe)
        ],
        new(true, false, true, true));

    public static ApiDescriptor Save { get; } = new(
        ApiIds.RoomSettingsSave,
        "Save room settings",
        "Atomically replaces one owned room's editable settings and waits for the terminal server acknowledgement.",
        ApiMemberKind.Operation,
        ApiExposure.All,
        typeof(RoomSettingsSaveRequest),
        typeof(RoomSettingsSaveReceipt),
        [
            new("settings", typeof(RoomSettingsValues), true, null, "Complete writable settings block including mute, kick and ban policies."),
            new("password", typeof(string), false, "", "Door password; an empty value clears an existing password.", new(MaxUtf8Bytes: ushort.MaxValue)),
            TimeoutParameter(),
            SessionGenerationParameter(),
            RoomGenerationParameter(),
            new("expected_operation_revision", typeof(long?), false, null, "Optional room-operation revision returned by a prior settings read.", new(Minimum: 1)),
            new("expected_snapshot_revision", typeof(long?), false, null, "Optional settings snapshot revision returned by a prior settings read.", new(Minimum: 1))
        ],
        [GameRequirement.HotelConnected],
        [new(GameRequirement.RoomSettingsLoaded, ApiStateChange.Invalidates)],
        [
            new(MessageKeys.Room.Settings.Save, MessageDirection.Out, ApiMessageRole.Send),
            new(MessageKeys.Room.Settings.SaveSucceeded, MessageDirection.In, ApiMessageRole.Observe),
            new(MessageKeys.Room.Settings.SaveFailed, MessageDirection.In, ApiMessageRole.Observe)
        ],
        new(false, false, true, true));

    public static ApiDescriptor Changed { get; } = new(
        ApiIds.RoomSettingsChanged,
        "Room settings changed",
        "Publishes bounded room-settings refresh, rejection, invalidation, acknowledgement and lifecycle summaries.",
        ApiMemberKind.Event,
        event_exposure,
        null,
        typeof(RoomSettingsChanged),
        stateEffects:
        [
            new(GameRequirement.RoomSettingsLoaded, ApiStateChange.Changes),
            new(GameRequirement.RoomSettingsLoaded, ApiStateChange.Invalidates)
        ],
        messages:
        [
            .. ObservedMessages(),
            new(MessageKeys.Room.Settings.Save, MessageDirection.Out, ApiMessageRole.Observe)
        ]);

    private static IReadOnlyList<ApiMessageRequirement> ObservedMessages() =>
    [
        new(MessageKeys.Room.Settings.Snapshot, MessageDirection.In, ApiMessageRole.Observe),
        new(MessageKeys.Room.Settings.RequestFailed, MessageDirection.In, ApiMessageRole.Observe),
        new(MessageKeys.Room.Settings.SaveSucceeded, MessageDirection.In, ApiMessageRole.Observe),
        new(MessageKeys.Room.Settings.SaveFailed, MessageDirection.In, ApiMessageRole.Observe)
    ];

    private static ApiParameter RoomIdParameter() => new(
        "room_id",
        typeof(Id),
        true,
        null,
        "Positive owned room identifier.",
        new(Pattern: "^[1-9][0-9]*$"));

    private static ApiParameter TimeoutParameter() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum total wait including lane contention and any permitted retry.",
        new(Minimum: 1, Maximum: 120000));

    private static ApiParameter SessionGenerationParameter() => new(
        "expected_session_generation",
        typeof(long?),
        false,
        null,
        "Optional active hotel-session generation guard.",
        new(Minimum: 0));

    private static ApiParameter RoomGenerationParameter() => new(
        "expected_room_generation",
        typeof(long?),
        false,
        null,
        "Optional guard used only when the target is the currently active room.",
        new(Minimum: 0));
}

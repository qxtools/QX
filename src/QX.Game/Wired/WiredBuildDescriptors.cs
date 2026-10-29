using Qx.Game.Api;
namespace Qx.Game.Wired;

internal static class WiredBuildDescriptors
{
    public static ApiDescriptor Guide { get; } = new(
        ApiIds.WiredBuildGuide, "Wired building guide",
        "Start here to build or inspect Wired. Returns the native plan/configuration workflow, source wiring rules, C# example, shared application member IDs and limits. No connection required.",
        ApiMemberKind.Query, ApiExposure.All, typeof(WiredBuildGuideRequest), typeof(WiredBuildGuide),
        toolHints: new(true, false, true, false), invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor Measure { get; } = new(
        ApiIds.WiredUsageMeasure, "Measure Wired usage",
        "Samples actual room usage and Heavy while the existing setup runs, compares error counts and returns peak, mean, initial and final budgets. Does not activate stacks. Measures the whole room, not the isolated cost of one setup.",
        ApiMemberKind.Operation, ApiExposure.All, typeof(WiredUsageMeasureRequest), typeof(WiredUsageMeasurement),
        [new("duration_milliseconds", typeof(int), false, 2000, "Measurement duration, 100 through 60000 milliseconds."),
         new("interval_milliseconds", typeof(int), false, 500, "Delay between samples, 100 through 5000 milliseconds.")],
        [GameRequirement.HotelConnected, GameRequirement.RoomActive], messages: monitor(), toolHints: new(true, false, true, true));

    public static ApiDescriptor Configure { get; } = new(
        ApiIds.WiredConfigure, "Configure Wired",
        "Reads the current form, validates the expected category and code, patches only supplied controls, saves changed settings or a requested snapshot refresh and verifies readback. Unknown fields survive. Dialogs are hidden by default.",
        ApiMemberKind.Operation, ApiExposure.All, typeof(WiredConfigureRequest), typeof(WiredFormView),
        [new("furni_id", typeof(Id), true, null, "Existing Wired furniture ID."),
         new("category", typeof(Qx.Model.Wired.WiredFormCategory), true, null, "Expected form category."),
         new("code", typeof(int), true, null, "Expected form code."),
         new("settings", typeof(WiredFormPatch), true, null, "Explicit controls to change."),
         new("capture_snapshot", typeof(bool), false, false, "Force a configuration save to refresh stored snapshot values, even when settings are unchanged. Does not restore furniture in the room."),
         new("options", typeof(WiredBuildOptions), false, null, "Visibility, timeout and readback options.")],
        [GameRequirement.HotelConnected, GameRequirement.RoomActive],
        messages: WiredDescriptors.FormGet.Messages.Concat(WiredDescriptors.FormSave.Messages), toolHints: new(false, false, false, true));

    public static ApiDescriptor Analyze { get; } = new(
        ApiIds.WiredBuildAnalyze, "Analyze Wired plan",
        "Checks typed settings, furniture keys and selector consumers without sending anything. Work estimates are not measured hotel usage.",
        ApiMemberKind.Query, ApiExposure.All, typeof(WiredBuildRequest), typeof(WiredBuildAnalysis),
        parameters(), toolHints: new(true, false, true, false), invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor Preflight { get; } = new(
        ApiIds.WiredBuildPreflight, "Check Wired build against room",
        "Validates the plan, exact furniture classes, existing IDs, permissions, live room cap and error baseline. Places and changes nothing.",
        ApiMemberKind.Operation, ApiExposure.All, typeof(WiredBuildRequest), typeof(WiredBuildPreflight),
        parameters(), [GameRequirement.HotelConnected, GameRequirement.RoomActive], messages: monitor(), toolHints: new(true, false, true, true));

    public static ApiDescriptor Build { get; } = new(
        ApiIds.WiredBuild, "Build Wired plan",
        "Resolves exact Builders Club classes once, reuses unambiguous furniture at planned positions or explicit IDs, verifies placements and states, resolves symbolic selection keys, saves typed forms and checks readback. Placements advance on native room confirmations; missing replies and placement-rate notifications adapt per-kind pacing. Settings dialogs are hidden by default. Trigger forms are configured last; existing triggers remain active. Returns partial progress on failure without deleting furniture. Retrying placements is bounded; late arrivals can require reconciliation.",
        ApiMemberKind.Operation, ApiExposure.All, typeof(WiredBuildRequest), typeof(WiredBuildResult),
        parameters(), [GameRequirement.HotelConnected, GameRequirement.RoomActive], messages: monitor()
            .Concat(WiredDescriptors.FormGet.Messages.Select(message => message with { Required = false }))
            .Concat(WiredDescriptors.FormSave.Messages.Select(message => message with { Required = false })),
        toolHints: new(false, false, false, true));

    public static ApiDescriptor Inspect { get; } = new(
        ApiIds.WiredInspect, "Inspect Wired forms",
        "Reads requested Wired forms sequentially with server-granted read permission. Preserves unknown fields and reports per-item errors. Dialogs are hidden by default; does not bypass owner permissions.",
        ApiMemberKind.Operation, ApiExposure.All, typeof(WiredInspectRequest), typeof(WiredInspectResult),
        [new("furniture_ids", typeof(IReadOnlyList<Id>), true, null, "Wired furniture IDs to inspect."),
         new("hide_settings", typeof(bool), false, true, "Suppress settings windows during this operation."),
         new("timeout_milliseconds", typeof(int), false, 10000, "Timeout per form, 1 through 120000.")],
        [GameRequirement.HotelConnected, GameRequirement.RoomActive],
        messages: WiredDescriptors.FormGet.Messages, toolHints: new(true, false, true, true));

    public static ApiDescriptor Progress { get; } = new(
        ApiIds.WiredBuildProgress, "Wired build progress", "Reports catalog, placement, configuration, inspection and completion progress.",
        ApiMemberKind.Event, ApiExposure.Scripting | ApiExposure.Ui | ApiExposure.Cli, null, typeof(WiredBuildProgress));

    private static ApiParameter[] parameters() => [
        new("items", typeof(IReadOnlyList<WiredBuildItem>), true, null, "Named furniture, exact class names, tile positions, typed form settings and symbolic furniture references."),
        new("options", typeof(WiredBuildOptions), false, null, "Dialog visibility, verification, bounded placement retries, timeout and live usage headroom."),
        new("workload", typeof(WiredWorkload), false, null, "Optional activation rate, actions, targets and receiver fanout. A measured cost coefficient must be calibrated in the current room.")
    ];

    private static IEnumerable<ApiMessageRequirement> monitor() =>
        WiredDescriptors.RoomStatsGet.Messages.Concat(WiredDescriptors.RoomErrorLogsGet.Messages);
}

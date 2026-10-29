using Qx.Game.Api;
using Qx.Game.Wired.Simulation;
using Qx.Game.Wired;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    public SimulationGuide GetWiredSimulationGuide() =>
        _application.Invoke<SimulationCoverageRequest, SimulationGuide>(ApiIds.SimulationGuide, new(), Ct);

    public SimulationSession CreateWiredSimulation(SimulationRoom room, SimulationOptions? options = null) =>
        _application.Invoke<SimulationCreateRequest, SimulationSession>(ApiIds.WiredSimulationCreate, new(room, options), Ct);

    public SimulationRoom SimulateWiredPlan(WiredBuildPlan plan, SimulationRoom room) => SimulationPlan.Apply(plan, room);

    public SimulationResult RunWiredSimulation(string session_id, IReadOnlyList<SimulationEvent>? events = null,
        long advance_milliseconds = 1000, int? maximum_events = null, IReadOnlyList<long>? breakpoints = null) =>
        _application.Invoke<SimulationRunRequest, SimulationResult>(ApiIds.WiredSimulationRun,
            new(session_id, events, advance_milliseconds, maximum_events, breakpoints), Ct);

    public SimulationResult InspectWiredSimulation(string session_id) =>
        _application.Invoke<SimulationSessionRequest, SimulationResult>(ApiIds.WiredSimulationInspect, new(session_id), Ct);

    public SimulationSession ForkWiredSimulation(string session_id) =>
        _application.Invoke<SimulationSessionRequest, SimulationSession>(ApiIds.WiredSimulationFork, new(session_id), Ct);

    public bool DeleteWiredSimulation(string session_id) =>
        _application.Invoke<SimulationSessionRequest, SimulationRemoved>(ApiIds.WiredSimulationDelete, new(session_id), Ct).Removed;

    public IReadOnlyList<SimulationAssertionResult> AssertWiredSimulation(string session_id, IReadOnlyList<SimulationAssertion> assertions) =>
        _application.Invoke<SimulationAssertRequest, IReadOnlyList<SimulationAssertionResult>>(ApiIds.WiredSimulationAssert, new(session_id, assertions), Ct);

    public SimulationRoom CaptureWiredSimulation(IReadOnlyList<WiredFormView>? forms = null,
        IReadOnlyList<SimulationVariable>? variables = null, IReadOnlyList<WiredVariablesObjectSnapshot>? object_values = null) =>
        _application.Invoke<SimulationCaptureRequest, SimulationRoom>(ApiIds.WiredSimulationCapture,
            new(forms, variables, object_values), Ct);

    public SimulationLeaderboardPage PreviewWiredLeaderboard(string session_id, long furniture_id, string? variable_id = null, int page = 1) =>
        _application.Invoke<SimulationLeaderboardRequest, SimulationLeaderboardPage>(ApiIds.WiredSimulationLeaderboardPreview,
            new(session_id, furniture_id, variable_id, page), Ct);
}

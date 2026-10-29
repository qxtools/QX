using Qx.Game.Wired.Simulation;

namespace Qx.Game.Wired.Simulation;

public sealed record SimulationCreateRequest(SimulationRoom Room, SimulationOptions? Options = null);
public sealed record SimulationSessionRequest(string SessionId);
public sealed record SimulationRunRequest(string SessionId, IReadOnlyList<SimulationEvent>? Events = null,
    long AdvanceMilliseconds = 1000, int? MaximumEvents = null, IReadOnlyList<long>? Breakpoints = null);
public sealed record SimulationAssertRequest(string SessionId, IReadOnlyList<SimulationAssertion> Assertions);
public sealed record SimulationCaptureRequest(IReadOnlyList<WiredFormView>? Forms = null,
    IReadOnlyList<SimulationVariable>? Variables = null,
    IReadOnlyList<WiredVariablesObjectSnapshot>? ObjectValues = null);
public sealed record SimulationSession(string SessionId, SimulationResult Result);
public sealed record SimulationRemoved(bool Removed);
public sealed record SimulationCoverageRequest;
public sealed record SimulationGuide(string Scope, IReadOnlyList<string> Workflow,
    IReadOnlyList<SimulationCoverage> Rules);
public sealed record SimulationPlanRequest(WiredBuildRequest Plan, SimulationRoom Room);
public sealed record SimulationLeaderboardRequest(string SessionId, long FurnitureId, string? VariableId = null, int Page = 1);

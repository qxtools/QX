using Qx.Model.Wired;

namespace Qx.Game.Wired.Simulation;

public enum SimulationApiOperation
{
    ReadValue, ReplaceValue, PatchValue, DeleteValue, ReadProfile, PatchProfile, Batch, BulkDelete
}

public sealed record SimulationApiAddress(string Kind, long ExternalId, long EntityId);

public sealed record SimulationApiAccess(bool Enabled = false, bool Read = false, bool Write = false,
    IReadOnlyList<SimulationApiAddress>? Addresses = null, bool AllowMassDeletion = false,
    bool AllHoldersKnown = false, bool? BulkDeleteNotifications = null);

public sealed record SimulationApiRequest(SimulationApiOperation Operation, WiredSourceDomain Domain = WiredSourceDomain.Global,
    string? Variable = null, string? TargetKind = null, long? EntityId = null, long? Value = null,
    IReadOnlyDictionary<string, long?>? Values = null, IReadOnlyList<SimulationApiRequest>? Operations = null,
    string? Id = null, IReadOnlyList<string>? Variables = null);

public sealed record SimulationApiResponse(int Status, long At, string? Id = null, string? Error = null,
    IReadOnlyDictionary<string, SimulationValue>? Values = null,
    IReadOnlyList<SimulationApiResponse>? Results = null);

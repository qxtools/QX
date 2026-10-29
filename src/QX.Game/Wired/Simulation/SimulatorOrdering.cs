using Qx.Model.Wired;

namespace Qx.Game.Wired.Simulation;

public sealed partial class Simulator
{
    private static bool static_empty_action(SimulationNode node) =>
        node.Name is "MoveAsGroup" or "RelativeFurniMove" or "MoveFurni" or "SetFurniAltitude" or "ToggleFurniState" or "ToggleToRandomState" &&
        furniture_source(node) == WiredFurnitureSource.Picked && (node.Furniture?.Count ?? 0) == 0;
}

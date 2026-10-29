namespace Qx.Game.Wired.Simulation;

internal static class SimulationGeometry
{
    internal static long height_value(double value) => checked((long)Math.Floor(value * 100 + 0.5));
}

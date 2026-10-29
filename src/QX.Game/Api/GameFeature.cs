namespace Qx.Game.Api;

internal interface IGameFeature : IDisposable
{
    IReadOnlyList<IApiBinding> Bindings { get; }
}

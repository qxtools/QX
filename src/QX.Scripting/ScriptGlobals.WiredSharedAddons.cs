using Qx.Game.Api;
using Qx.Game.Wired;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    public Task<WiredDispatchResult> SynchronizeWiredSharedAddons(WiredFormView review)
    {
        ArgumentNullException.ThrowIfNull(review);
        return wired_call<WiredSharedAddonsSynchronizeRequest, WiredDispatchResult>(
            ApiIds.WiredSharedAddonsSynchronize,
            new(review.Configuration.Id, review.Generation, review.Revision));
    }
}

using Qx.Game.Api;
using Qx.Protocol;

namespace Qx.Game.Wired;

internal static partial class WiredDescriptors
{
    public static ApiDescriptor SharedAddonsSynchronize { get; } = Call<WiredSharedAddonsSynchronizeRequest, WiredDispatchResult>(
        ApiIds.WiredSharedAddonsSynchronize,
        "Synchronize shared variable add-ons",
        "Sends the separate synchronization command for a freshly reviewed Variable Add-on Share. Requires current Wired write permission. Does not save category settings or acknowledge remote synchronization; the result confirms dispatch only. Opens no settings dialog.",
        [new("furni_id", typeof(Id), true, null, "Reviewed Variable Add-on Share furniture identifier, from 1 to 2147483647.", IdConstraint()),
         new("expected_generation", typeof(long), true, null, "Generation from the current form review."),
         new("expected_revision", typeof(long), true, null, "Revision from the current form review.")],
        [Send(MessageKeys.Wired.SharedAddons.Synchronize)], WriteHints(false, false));
}

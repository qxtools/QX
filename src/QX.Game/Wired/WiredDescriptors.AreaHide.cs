using Qx.Game.Api;
using Qx.Model.Wired;
using Qx.Protocol;

namespace Qx.Game.Wired;

internal static partial class WiredDescriptors
{
    public static ApiDescriptor AreaHideGet { get; } = Call<WiredAreaHideGetRequest, WiredAreaHideView>(
        ApiIds.WiredAreaHideGet, "Read area-hide settings", "Reads the local furniture editor and current floor-plan region without sending.",
        [new("furni_id", typeof(Id), true, null, "Area-hide furniture identifier.", IdConstraint())], [], new(true, false, true, false));

    public static ApiDescriptor AreaHideSet { get; } = Call<WiredAreaHideSetRequest, WiredDispatchResult>(
        ApiIds.WiredAreaHideSet, "Set area-hide settings", "Sends a complete area-hide rectangle and options while the effect is off. Dispatch is not a server acknowledgement.",
        [new("update", typeof(SetAreaHideData), true, null, "Complete settings, excluding the on/off state."),
         new("expected_room_generation", typeof(long), true, null, "Room generation from the editor read.")],
        [Send(MessageKeys.Room.Environment.AreaHideSet)], WriteHints(false, true));

    public static ApiDescriptor AreaHideToggle { get; } = Call<WiredAreaHideToggleRequest, WiredDispatchResult>(
        ApiIds.WiredAreaHideToggle, "Toggle area hiding", "Uses the area-hide furniture with parameter zero, as the client's on/off button does. A controller hidden from the room's furniture is accepted while the floor plan lists its area.",
        [new("furni_id", typeof(Id), true, null, "Area-hide furniture identifier.", IdConstraint()),
         new("expected_room_generation", typeof(long), true, null, "Room generation from the editor read or the current room.")],
        [Send(MessageKeys.Room.FloorItem.Use)], WriteHints(false, false));
}

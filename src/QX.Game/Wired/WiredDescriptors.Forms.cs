using Qx.Game.Api;
using Qx.Model.Wired;
using Qx.Protocol;

namespace Qx.Game.Wired;

internal static partial class WiredDescriptors
{
    public static ApiDescriptor FormGet { get; } = Call<WiredConfigurationGetRequest, WiredFormView>(
        ApiIds.WiredFormGet, "Get typed Wired form",
        "Opens a configuration and returns named fields, choices, practical usage rules with evidence levels, and a revision token for saving.",
        [new("furni_id", typeof(Id), true, null, "Positive Wired furniture identifier.", IdConstraint()), TimeoutParameter()],
        [Send(MessageKeys.Wired.Configuration.OpenRequest),
         Observe(MessageKeys.Wired.Configuration.Trigger, false), Observe(MessageKeys.Wired.Configuration.Action, false),
         Observe(MessageKeys.Wired.Configuration.Condition, false), Observe(MessageKeys.Wired.Configuration.Selector, false),
         Observe(MessageKeys.Wired.Configuration.Addon, false), Observe(MessageKeys.Wired.Configuration.Variable, false)], ReadHints());

    public static ApiDescriptor FormSave { get; } = Call<WiredFormSaveRequest, WiredConfigurationSaveResult>(
        ApiIds.WiredFormSave, "Save typed Wired form",
        "Applies named fields and sources to the reviewed configuration. Rejects stale reviews before sending.",
        [new("furni_id", typeof(Id), true, null, "Reviewed furniture identifier.", IdConstraint()),
         new("expected_generation", typeof(long), true, null, "Generation from the form read."),
         new("expected_revision", typeof(long), true, null, "Revision from the form read."),
         new("patch", typeof(WiredFormPatch), true, null, "Named fields, typed sources and category controls to change."), TimeoutParameter()],
        [new(MessageKeys.Wired.Configuration.TriggerUpdate, MessageDirection.Out, ApiMessageRole.Send, false),
         new(MessageKeys.Wired.Configuration.ActionUpdate, MessageDirection.Out, ApiMessageRole.Send, false),
         new(MessageKeys.Wired.Configuration.ConditionUpdate, MessageDirection.Out, ApiMessageRole.Send, false),
         new(MessageKeys.Wired.Configuration.SelectorUpdate, MessageDirection.Out, ApiMessageRole.Send, false),
         new(MessageKeys.Wired.Configuration.AddonUpdate, MessageDirection.Out, ApiMessageRole.Send, false),
         new(MessageKeys.Wired.Configuration.VariableUpdate, MessageDirection.Out, ApiMessageRole.Send, false),
         Observe(MessageKeys.Wired.Configuration.SaveSucceeded), Observe(MessageKeys.Wired.Configuration.ValidationFailed)], WriteHints(false, true));

    public static ApiDescriptor FormDefinitions { get; } = new(
        ApiIds.WiredFormDefinitions, "Wired form definitions", "Reads typed forms, field constraints and English usage knowledge: setup, target sources, companion furniture and pitfalls, with evidence levels.",
        ApiMemberKind.Query, ApiExposure.All, typeof(WiredFormDefinitionsRequest), typeof(IReadOnlyList<WiredFormDefinition>),
        [new("category", typeof(WiredFormCategory?), false, null, "Optional form category."),
         new("code", typeof(int?), false, null, "Optional primary or alias code.")],
        toolHints: new(true, false, true, false), invocationScope: ApiLifetime.Persistent);

    public static ApiDescriptor FxStyles { get; } = new(
        ApiIds.WiredFxStyles, "Wired FX styles", "Reads all 38 verified styles with allowed colors, widths and renderers.",
        ApiMemberKind.Query, ApiExposure.All, typeof(WiredFxStylesRequest), typeof(IReadOnlyList<WiredFxStyleDefinition>),
        [new("category", typeof(WiredFxCategory?), false, null, "Optional FX category.")],
        toolHints: new(true, false, true, false), invocationScope: ApiLifetime.Persistent);
}

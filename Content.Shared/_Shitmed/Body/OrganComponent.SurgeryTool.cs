using Content.Shared._Shitmed.Medical.Surgery.Tools;

namespace Content.Shared.Body;

/// <summary>
/// Makes every organ entity usable as a surgery tool, additively (a separate partial-class
/// file rather than touching core Content.Shared/Body/OrganComponent.cs — matches this
/// port's Phase 0 policy of not modifying core Body). Needed for the "Insert Organ"/"Insert
/// Part" surgery steps (SurgeryStepInsertOrgan/SurgeryStepInsertFeature), whose `tool:`
/// ComponentRegistry lists a plain `type: Organ` entry — the tool being used IS the severed
/// limb/organ itself. Without this, SharedSurgerySystem.Steps.cs's tool-resolution logic
/// rejects it at runtime ("wants bad component ... which isn't a ISurgeryTool") and the step
/// can never be performed.
/// </summary>
public sealed partial class OrganComponent : ISurgeryToolComponent
{
    public string ToolName => "an organ";

    [DataField]
    public bool? Used { get; set; }

    [DataField]
    public float Speed { get; set; } = 1f;
}

using Content.Shared.Tools;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>
/// The trainee carries tools with these qualities. Matching on quality rather than prototype lets a
/// red crowbar picked up three rooms ago count just as much as the one from the vendor.
/// </summary>
public sealed partial class ToolQualitiesCondition : TutorialCondition
{
    [DataField(required: true)]
    public List<ProtoId<ToolQualityPrototype>> Qualities = new();

    /// <summary>Only look inside whatever is worn in this slot.</summary>
    [DataField]
    public string? Slot;

    /// <summary>Look everywhere except inside whatever is worn in this slot.</summary>
    [DataField]
    public string? OutsideSlot;

    /// <summary>False means any one of the qualities is enough.</summary>
    [DataField]
    public bool RequireAll = true;
}

using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial.Prototypes;

/// <summary>
/// What one machine needs from the lathe. While its window is open the missing recipes glow and a checklist
/// sits beside it; <c>LatheGuideGatheredCondition</c> checks the same list.
/// </summary>
[DataDefinition]
public sealed partial class TutorialLatheGuide
{
    [DataField(required: true)]
    public string Lathe = string.Empty;

    /// <summary>What the parts are for, as the card names it.</summary>
    [DataField(required: true)]
    public LocId Machine;

    [DataField(required: true)]
    public List<TutorialLatheItem> Items = new();

    /// <summary>
    /// Anchors whose surroundings count as gathered as well, besides the lathe tray and the trainee's own
    /// pockets. Parts already put into a frame standing on one of them count too.
    /// </summary>
    [DataField]
    public List<string> CountAt = new();
}

[DataDefinition]
public sealed partial class TutorialLatheItem
{
    [DataField(required: true)]
    public ProtoId<LatheRecipePrototype> Recipe;

    /// <summary>Stacks count by amount, so ten metres of cable is ten, not one coil.</summary>
    [DataField]
    public int Count = 1;
}

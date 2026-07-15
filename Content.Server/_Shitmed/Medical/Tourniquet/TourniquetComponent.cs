using Content.Shared.Body;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._Shitmed.Medical.Tourniquet;

[RegisterComponent]
public sealed partial class TourniquetComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? OrganTourniqueted;

    /// <summary>
    /// How long it takes to put the tourniquet on.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public float Delay = 5f;

    /// <summary>
    /// How long it takes to take the tourniquet off.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public float RemoveDelay = 7f;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public HashSet<ProtoId<OrganCategoryPrototype>> BlockedCategories = new();

    /// <summary>
    ///     Sound played on putting the tourniquet on
    /// </summary>
    [DataField("putOnSound")]
    public SoundSpecifier? TourniquetPutOnSound = null;

    /// <summary>
    ///     Sound played on taking the tourniquet off
    /// </summary>
    [DataField("putOffSound")]
    public SoundSpecifier? TourniquetPutOffSound = null;
}

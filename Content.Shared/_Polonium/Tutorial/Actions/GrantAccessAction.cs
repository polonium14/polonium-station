using Content.Shared.Access;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial.Actions;

/// <summary>Slaps extra access tags onto the player's ID card.</summary>
public sealed partial class GrantAccessAction : TutorialAction
{
    [DataField(required: true)]
    public HashSet<ProtoId<AccessLevelPrototype>> Tags = new();
}

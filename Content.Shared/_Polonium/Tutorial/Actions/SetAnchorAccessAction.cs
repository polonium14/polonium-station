using Content.Shared.Access;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial.Actions;

/// <summary>
/// Rewrites the anchor's access reader and locks it. Any one of the listed levels opens it.
/// </summary>
public sealed partial class SetAnchorAccessAction : TutorialAction
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    [DataField(required: true)]
    public List<ProtoId<AccessLevelPrototype>> Access = new();
}

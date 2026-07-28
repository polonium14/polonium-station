using Robust.Shared.Prototypes;

namespace Content.Server.Abilities.Felinid;

[RegisterComponent]
public sealed partial class FelinidComponent : Component
{
    /// <summary>
    /// The hairball prototype to use.
    /// </summary>
    [DataField]
    public EntProtoId HairballPrototype = "Hairball";

    [DataField]
    public EntProtoId? HairballActionId = "ActionHairball";

    [DataField]
    public EntityUid? HairballAction;

    [DataField]
    public EntProtoId? EatActionId = "ActionEatMouse";

    [DataField]
    public EntityUid? EatAction;

    [DataField]
    public EntityUid? EatActionTarget;
}

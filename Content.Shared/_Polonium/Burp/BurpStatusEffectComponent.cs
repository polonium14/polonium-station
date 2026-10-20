using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Burp;

[RegisterComponent]
public sealed partial class BurpStatusEffectComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Action;

    [DataField]
    public EntityUid? ActionEntity;
}

public sealed partial class BurpActionEvent : InstantActionEvent;

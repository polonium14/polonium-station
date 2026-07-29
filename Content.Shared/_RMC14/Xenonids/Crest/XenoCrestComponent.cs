using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Crest;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoCrestSystem))]
public sealed partial class XenoCrestComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Lowered;

    // RMC does armor - we just cut incoming dmg
    [DataField, AutoNetworkedField]
    public float DamageModifier = 0.75f;

    [DataField, AutoNetworkedField]
    public float SpeedMultiplier = 0.7f;
}

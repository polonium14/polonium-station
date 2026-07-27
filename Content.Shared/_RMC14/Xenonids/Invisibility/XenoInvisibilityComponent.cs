using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Invisibility;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoInvisibilitySystem))]
public sealed partial class XenoInvisibilityComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField, AutoNetworkedField]
    public float CloakVisibility = 0.1f;

    // full bar lasts about this long - drain scales off max plasma
    [DataField, AutoNetworkedField]
    public TimeSpan MaxCloakDuration = TimeSpan.FromSeconds(30);
}
